using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Drawing;
using System.Diagnostics;

namespace gInk
{
    public class APIRest
    {
        public static HttpListener http;
        public static Root Root;
        Task listenTask=null;
        public APIRest(Root root)
        {
            Root = root;
            http = new HttpListener();

            if(Root.APIRestUrl !="")
                ChangeAddress(Root.APIRestUrl);

            //listenTask.GetAwaiter().GetResult();
            //listenTask.Start();
        }

        public void Close()
        {
            http.Close();
        }

        [DebuggerNonUserCode]
        public bool ChangeAddress(string addr)
        {
            http.Stop();
            http.Prefixes.Clear();
            try
            {
                string a = addr;
                if (!a.EndsWith("/"))
                    a += "/";
                http.Prefixes.Add(a);
                return Start();                
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return false;
            }
        }

        public string GetAddress()
        {
            return http.Prefixes.First();
        }

        public bool IsListening()
        {
            return http.IsListening;
        }

        public bool Start()
        {
            try
            {
                http.Start();
                try
                {
                    listenTask?.Dispose();
                }
                catch { }
                listenTask=HandleIncomingConnections();

                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool Stop()
        {
            try
            {
                http.Stop();
                if(!listenTask.Wait(1000))
                    throw new Exception("Can not kill listenTask");
                return true;
            }
            catch(Exception e)
            {
                throw e;
            }
        }

        public static async Task HandleIncomingConnections()
        {
            Dictionary<string,string> ParseQuery(string query)
            {
                Dictionary<string, string> lst = new Dictionary<string, string>();
                if (!query.StartsWith("?"))
                    return lst;
                string[] r;
                foreach(string s in query.Substring(1).Split('&'))
                {
                    r = s.Split(new char[] { '=' }, 2);
                    lst.Add(r[0], r.Length == 1?"":r[1]);
                }
                return lst;
            }
            bool runServer = true;

            int requestCount = 0;
            // While a user hasn't visited the `shutdown` url, keep on handling requests
            while (runServer)
            {
                // Will wait here until we hear from a connection
                HttpListenerContext ctx;
                try
                {
                    ctx = await http.GetContextAsync();
                }
                catch (HttpListenerException)
                {
                    runServer = false;
                    break;
                }
                if (!http.IsListening)
                {
                    runServer = false;
                    break;
                }
                // Peel out the requests and response objects
                HttpListenerRequest req = ctx.Request;
                HttpListenerResponse resp = ctx.Response;
                string ret = "!!! unassigned ????";
                try
                {
                    resp.StatusCode = 200;
                    // Print out some info about the request
                    Console.WriteLine("Request #: {0}", ++requestCount);
                    Console.WriteLine(req.Url.ToString());
                    Console.WriteLine(req.HttpMethod);
                    Console.WriteLine();

                    Dictionary<string, string> query = ParseQuery(Uri.UnescapeDataString(req.Url.Query));

                    if (req.Url.AbsolutePath == "/Inking")
                    {
                        string s;
                        if (query.TryGetValue("S", out s))
                        {
                            s = s.ToLower();
                            if (s == "true")
                                Root.StartInk();
                            else if (s == "false")
                                Root.StopInk();
                            else
                            {
                                resp.StatusCode = 400;
                                ret = string.Format("!!!! Error in Query ({0}) - {1} ", req.HttpMethod, req.Url.AbsoluteUri);
                            }
                        }
                        if (resp.StatusCode == 200)
                            ret = " { \"Started\" : " + ((Root.FormDisplay.Visible || Root.FormCollection.Visible)?"true":"false") + " }";
                    }


                    else if (req.Url.AbsolutePath == "/PenDef")
                    {
                        string s;
                        int i = 0, r, g, b, w;
                        byte t;
                        float f;
                        string ff = "";
                        if (!(Root.FormDisplay.Visible || Root.FormCollection.Visible))
                        {
                            resp.StatusCode = 409;
                            ret = "!!!!! Not in Inking mode";
                        }
                        else if (query.TryGetValue("P", out s) && int.TryParse(s, out i) && -1 <= i && i <= 9)
                        {
                            if (i == -1)
                                i = Root.CurrentPen;
                            if (query.ContainsKey("Browse"))
                            {
                                Root.FormCollection.btColor_LongClick(Root.FormCollection.btPen[i]);
                            }
                            if (query.ContainsKey("R"))
                            {
                                if (query.TryGetValue("R", out s) && int.TryParse(s, out r) && 0 <= r && r <= 255 &&
                                    query.TryGetValue("G", out s) && int.TryParse(s, out g) && 0 <= g && g <= 255 &&
                                    query.TryGetValue("B", out s) && int.TryParse(s, out b) && 0 <= b && b <= 255)
                                    Root.PenAttr[i].Color = Color.FromArgb(r, g, b);
                                else
                                    resp.StatusCode = 400;
                            }
                            if (query.ContainsKey("T"))
                            {
                                if (query.TryGetValue("T", out s) && byte.TryParse(s, out t) && 0 <= t && t <= 255)
                                    Root.PenAttr[i].Transparency = t;
                                else
                                    resp.StatusCode = 400;
                            }
                            if (query.ContainsKey("W"))
                            {
                                if (query.TryGetValue("W", out s) && int.TryParse(s, out w) && 0 <= w && w <= 255)
                                    Root.PenAttr[i].Width = w;
                                else
                                    resp.StatusCode = 400;
                            }
                            if (query.ContainsKey("F"))
                            {
                                query.TryGetValue("F", out ff);
                                ff = ff.ToLower();
                                if (ff == "false")
                                { try { Root.PenAttr[i].ExtendedProperties.Remove(Root.FADING_PEN); } catch { }; }
                                else if (ff == "true")
                                    Root.PenAttr[i].ExtendedProperties.Add(Root.FADING_PEN, Root.TimeBeforeFading);
                                else if (float.TryParse(ff, out f))
                                    Root.PenAttr[i].ExtendedProperties.Add(Root.FADING_PEN, f);
                                else
                                    resp.StatusCode = 400;
                            }
                            if(i== Root.CurrentPen)
                            {
                                Root.SelectPen(Root.CurrentPen);
                            }
                            Root.FormCollection.btPen[i].BackgroundImage = Root.FormCollection.buildPenIcon(Root.PenAttr[i].Color, Root.PenAttr[i].Transparency, i == Root.CurrentPen,
                                                                                                            Root.PenAttr[i].ExtendedProperties.Contains(Root.FADING_PEN));
                            Root.UponButtonsUpdate |= 0x2;
                        }
                        if (resp.StatusCode == 200)
                        {
                            if (Root.PenAttr[i].ExtendedProperties.Contains(Root.FADING_PEN))
                            {
                                f = (float)(Root.PenAttr[i].ExtendedProperties[Root.FADING_PEN].Data);
                                if (f == Root.TimeBeforeFading)
                                    ff = "true";
                                else if (f <= 0)
                                    ff = "false";
                                else
                                    ff = f.ToString();
                            }
                            else
                                ff = "false";
                            ret = string.Format("{{\"Pen\":{0},\n\"Red\":{1}, \"Green\":{2}, \"Blue\":{3}, \"Transparency\":{4},\n\"Width\":{5},\n\"Fading\":{6},\n\"Enabled\":{7}\n}}",
                                                i, Root.PenAttr[i].Color.R, Root.PenAttr[i].Color.G, Root.PenAttr[i].Color.B, Root.PenAttr[i].Transparency,
                                                Root.PenAttr[i].Width, ff, Root.PenEnabled[i]?"true":"false");
                        }
                        else if (resp.StatusCode == 400)
                            ret = string.Format("!!!! Error in Query ({0}) - {1} ", req.HttpMethod, req.Url.AbsoluteUri);
                    }


                    else if (req.Url.AbsolutePath == "/CurrentPen")
                    {
                        string s;
                        int i = 0;
                        if (!(Root.FormDisplay.Visible || Root.FormCollection.Visible))
                        {
                            resp.StatusCode = 409;
                            ret = "!!!!! Not in Inking mode";
                        }
                        else if (query.TryGetValue("P", out s) && int.TryParse(s, out i) && 0 <= i && i <= 9)
                        {
                            Root.SelectPen(i);
                        }
                        else
                            resp.StatusCode = 400;

                        if (resp.StatusCode == 200)
                        {
                            ret = string.Format("{{\"Pen\":{0} }}", i);
                        }
                        else if(resp.StatusCode == 400)
                            ret = string.Format("!!!! Error in Query ({0}) - {1} ", req.HttpMethod, req.Url.AbsoluteUri);
                    }


                    else if (req.Url.AbsolutePath == "/CurrentTool")
                    {
                        string s;
                        int i = 0, f = 0;
                        if (!(Root.FormDisplay.Visible || Root.FormCollection.Visible))
                        {
                            resp.StatusCode = 409;
                            ret = "!!!!! Not in Inking mode";
                        }
                        else if (query.TryGetValue("T", out s) && int.TryParse(s, out i))
                        {
                            if (i == -3 || i == -2 || i == -1)
                                Root.SelectPen(i);
                            if (!query.ContainsKey("F"))
                                f = -1;
                            else if (!(query.TryGetValue("F", out s) && int.TryParse(s, out f) && -1 <= f && f < Filling.Modulo))
                                resp.StatusCode = 400;
                            if(i == Tools.ClipArt)
                                if (query.TryGetValue("I", out s))
                                {
                                    if (s.Contains('\\'))
                                        s = s.Replace('\\', '/');
                                    if (s.Contains('/'))
                                        s = Root.FormCollection.ClipartsDlg.LoadImage(s);
                                    Root.ImageStamp = Root.FormCollection.ClipartsDlg.getClipArtData(s);
                                    Root.ImageStamp.Filling = f;
                                }
                                else
                                {
                                    Root.FormCollection.MouseTimeDown = DateTime.FromBinary(0);
                                    Root.FormCollection.btTool_Click(Root.FormCollection.btClipArt, null);
                                }
                            bool b = false;
                            foreach (int j in Tools.All)
                            {
                                if (j == i)
                                {
                                    b = true;
                                    break;
                                }
                            }

                            if (b && resp.StatusCode == 200)
                            {
                                Root.FormCollection.SavedPen = -1;
                                Root.FormCollection.SavedTool = -1;
                                Root.FormCollection.SavedFilled = -1;
                                if (i >= Tools.Hand)
                                    Root.SelectPen(Root.FormCollection.LastPenSelected);
                                Root.FormCollection.SelectTool(i, f);

                            }

                            if (Root.FormCollection.Visible)
                                Root.UponButtonsUpdate |= 0x2;
                        }
                        if (resp.StatusCode == 200)
                        {
                            if (Root.EraserMode)
                            {
                                ret = string.Format("{{\"Tool\":{0},\"ToolInText\":\"{2}\", \"Filling\":{1}, \"FillingInText\":\"{3}\" }}", -1, -1, "Eraser", "-");
                            }
                            else if (Root.PointerMode)
                            {
                                ret = string.Format("{{\"Tool\":{0},\"ToolInText\":\"{2}\", \"Filling\":{1}, \"FillingInText\":\"{3}\" }}", -1, -1, "Pointer", "-");
                            }
                            else if (Root.PanMode)
                            {
                                ret = string.Format("{{\"Tool\":{0},\"ToolInText\":\"{2}\", \"Filling\":{1}, \"FillingInText\":\"{3}\" }}", -1, -1, "Pan", "-");
                            }
                            else
                            {
                                string st_i="";
                                if (Root.ToolSelected == Tools.ClipArt)
                                    st_i = string.Format(",\n \"Image\":\"{0}\" ", Root.ImageStamp.ImageStamp);
                                f = Root.ToolSelected == Tools.ClipArt ? Root.ImageStamp.Filling : Root.FilledSelected;
                                ret = string.Format("{{\"Tool\":{0},\"ToolInText\":\"{2}\", \"Filling\":{1}, \"FillingInText\":\"{3}\"{4} }}",
                                                        Root.ToolSelected, f , Tools.Names[Array.IndexOf(Tools.All,Root.ToolSelected)], Filling.Names[f+1],st_i);
                            }
                        }
                        else if (resp.StatusCode == 400)
                            ret = string.Format("!!!! Error in Query ({0}) - {1} ", req.HttpMethod, req.Url.AbsoluteUri);
                    }


                    else if (req.Url.AbsolutePath == "/EnlargePen")
                    {
                        string s;
                        int i;
                        if (!(Root.FormDisplay.Visible || Root.FormCollection.Visible))
                        {
                            resp.StatusCode = 409;
                            ret = "!!!!! Not in Inking mode";
                        }
                        else if (query.TryGetValue("D", out s) && int.TryParse(s, out i))
                        {
                            Root.FormCollection.PenWidth_Change(i);
                        }
                        else
                        {
                            resp.StatusCode = 400;
                            ret = string.Format("!!!! Error in Query ({0}) - {1} ", req.HttpMethod, req.Url.AbsoluteUri);
                        }
                        if (resp.StatusCode == 200)
                            ret = " { \"Width\" : " + Root.FormCollection.IC.DefaultDrawingAttributes.Width.ToString() + " }";
                    }


                    else if (req.Url.AbsolutePath == "/Magnet")
                    {
                        string s;
                        if (!(Root.FormDisplay.Visible || Root.FormCollection.Visible))
                        {
                            resp.StatusCode = 409;
                            ret = "!!!!! Not in Inking mode";
                        }
                        else if (query.TryGetValue("M", out s))
                        {
                            s = s.ToLower();
                            if (s == "true")
                                Root.MagneticRadius = Math.Abs(Root.MagneticRadius);
                            else if (s == "false")
                                Root.MagneticRadius = -Math.Abs(Root.MagneticRadius);
                            else
                            {
                                resp.StatusCode = 400;
                                ret = string.Format("!!!! Error in Query ({0}) - {1} ", req.HttpMethod, req.Url.AbsoluteUri);
                            }
                        }
                        if (resp.StatusCode == 200)
                        {
                            ret = " { \"Magnet\" : " + (Root.MagneticRadius>0?"true":"false") + " }";
                        }
                    }


                    else if (req.Url.AbsolutePath == "/VisibleInk")
                    {
                        string s;
                        if (!(Root.FormDisplay.Visible || Root.FormCollection.Visible))
                        {
                            resp.StatusCode = 409;
                            ret = "!!!!! Not in Inking mode";
                        }
                        else if (query.TryGetValue("V", out s))
                        {
                            s = s.ToLower();
                            if (s == "true")
                                Root.SetInkVisible(true);
                            else if (s == "false")
                                Root.SetInkVisible(false);
                            else
                            {
                                resp.StatusCode = 400;
                                ret = string.Format("!!!! Error in Query ({0}) - {1} ", req.HttpMethod, req.Url.AbsoluteUri);
                            }
                        }
                        if (resp.StatusCode == 200)
                            ret = " { \"VisibleInk\" : " + (Root.InkVisible?"true":"false") + " }";
                    }


                    else if (req.Url.AbsolutePath == "/Fold")
                    {
                        string s;
                        if (!(Root.FormDisplay.Visible || Root.FormCollection.Visible))
                        {
                            resp.StatusCode = 409;
                            ret = "!!!!! Not in Inking mode";
                        }
                        else if (query.TryGetValue("F", out s))
                        {
                            s = s.ToLower();
                            if (s == "true")
                                Root.Dock();
                            else if (s == "false")
                                Root.UnDock();
                            else
                            {
                                resp.StatusCode = 400;
                                ret = string.Format("!!!! Error in Query ({0}) - {1} ", req.HttpMethod, req.Url.AbsoluteUri);
                            }
                        }
                        if (resp.StatusCode == 200)
                            ret = " { \"Folded\" : " + (Root.Docked?"true":"false") + " }";
                    }


                    else if (req.Url.AbsolutePath == "/LoadStrokes")
                    {
                        string s;
                        if (!(Root.FormDisplay.Visible || Root.FormCollection.Visible))
                        {
                            resp.StatusCode = 409;
                            ret = "!!!!! Not in Inking mode";
                        }
                        else if (query.TryGetValue("F", out s))
                        {
                            if (s == "-")
                            {
                                Root.FormCollection.MouseTimeDown = DateTime.FromBinary(0);
                                Root.FormCollection.btLoad_Click(Root.FormCollection.btLoad, null);
                                ret = "{ \"OK\": true }";
                            }
                            try
                            {
                                Root.FormCollection.LoadStrokes(s);
                                ret = "{ \"OK\": true }";
                            }
                            catch(Exception e)
                            {
                                resp.StatusCode = 500;
                                ret = string.Format("!!!! error : "+e.Message);
                            }
                            Root.UponAllDrawingUpdate = true;
                        }
                        else
                        {
                            resp.StatusCode = 400;
                            ret = string.Format("!!!! Error in Query ({0}) - {1} ", req.HttpMethod, req.Url.AbsoluteUri);
                        }
                    }


                    else if (req.Url.AbsolutePath == "/SaveStrokes")
                    {
                        string s;
                        if (!(Root.FormDisplay.Visible || Root.FormCollection.Visible))
                        {
                            resp.StatusCode = 409;
                            ret = "!!!!! Not in Inking mode";
                        }
                        else if (query.TryGetValue("F", out s))
                        {
                            if (s == "-")
                            {
                                Root.FormCollection.MouseTimeDown = DateTime.FromBinary(0);
                                Root.FormCollection.btSave_Click(Root.FormCollection.btSave, null);
                                ret = "{ \"OK\": true }";
                            }
                            else
                            {
                                Root.FormCollection.SaveStrokes(s);
                                ret = "{ \"OK\": true }";
                            }
                        }
                        else
                        {
                            resp.StatusCode = 400;
                            ret = string.Format("!!!! Error in Query ({0}) - {1} ", req.HttpMethod, req.Url.AbsoluteUri);
                        }
                    }   


                    else if (req.Url.AbsolutePath == "/ClearScreen")
                    {
                        string s;
                        //int i = 0;
                        if (!(Root.FormDisplay.Visible || Root.FormCollection.Visible))
                        {
                            resp.StatusCode = 409;
                            ret = "!!!!! Not in Inking mode";
                        }
                        else if (query.TryGetValue("B", out s))
                        {
                            Root.FormCollection.MouseTimeDown = DateTime.Now;
                            s = s.ToLower();
                            if (s == "tr")//Transparent
                                Root.BoardSelected = 0;
                            else if (s == "wh")//White
                                Root.BoardSelected = 1;
                            else if (s == "cu")//Customized
                                Root.BoardSelected = 2;
                            else if (s == "bk")//Black
                                Root.BoardSelected = 3;
                            else if (s == "me")//Menu
                                Root.FormCollection.MouseTimeDown = DateTime.FromBinary(0);
                            else
                                resp.StatusCode = 400;
                        }
                        else
                            Root.FormCollection.MouseTimeDown = DateTime.Now;

                        if (resp.StatusCode == 200)
                        {
                            Root.FormCollection.btClear_Click(Root.FormCollection.btClear, null);
                            ret = "{ \"OK\": true }";
                        }
                        else if(resp.StatusCode == 400)
                            ret = string.Format("!!!! Error in Query ({0}) - {1} ", req.HttpMethod, req.Url.AbsoluteUri);
                    }


                    else if (req.Url.AbsolutePath == "/Magnify")
                    {
                        string s;
                        //int i = 0;
                        if (!(Root.FormDisplay.Visible || Root.FormCollection.Visible))
                        {
                            resp.StatusCode = 409;
                            ret = "!!!!! Not in Inking mode";
                        }
                        else if (query.TryGetValue("Z", out s))
                        {
                            s = s.ToLower();
                            if (s == "no")//Stop magnify
                                Root.FormCollection.StopAllZooms();
                            else if (s == "dyn")//dynamic Magnifier
                                Root.FormCollection.ActivateZoomDyn();
                            else if (s == "capt")//Capture
                                Root.FormCollection.StartZoomCapt();
                            else
                                resp.StatusCode = 400;
                        }


                        if (resp.StatusCode == 200)
                        {
                            if (Root.FormCollection.ZoomCaptured || Root.FormCollection.ZoomCaptured)
                                s = "Capt";
                            else if (Root.FormCollection.ZoomForm.Visible)
                                s = "Dyn";
                            else
                                s = "No";
                            ret = "{ \"Zoom\": \""+s+"\" }";
                        }
                        else if (resp.StatusCode == 400)
                            ret = string.Format("!!!! Error in Query ({0}) - {1} ", req.HttpMethod, req.Url.AbsoluteUri);
                    }


                    else if (req.Url.AbsolutePath == "/Snapshot")
                    {
                        string s;
                        if (!(Root.FormDisplay.Visible || Root.FormCollection.Visible))
                        {
                            resp.StatusCode = 409;
                            ret = "!!!!! Not in Inking mode";
                        }
                        else if (query.TryGetValue("A", out s))
                        {
                            s = s.ToLower();
                            if (s == "out")//Exit Inking after
                            {
                                Root.APIRestCloseOnSnap = true;
                                Root.FormCollection.btSnap_Click(null, null);
                            }
                            else if (s == "end")//dynamic Magnifier
                            {
                                Root.FormCollection.btSnap_Click(null, null);
                            }
                            else if (s == "cont")//continue after
                            {
                                Root.FormCollection.MouseTimeDown = DateTime.FromBinary(0);
                                Root.FormCollection.btSnap_Click(Root.FormCollection.btSnap, null);
                            }
                            else
                                resp.StatusCode = 400;
                        }


                        if (resp.StatusCode == 200)
                        {
                            ret = "{ \"OK\": true }";
                        }
                        else if (resp.StatusCode == 400)
                            ret = string.Format("!!!! Error in Query ({0}) - {1} ", req.HttpMethod, req.Url.AbsoluteUri);
                    }


                    else if (req.Url.AbsolutePath == "/ArrowCursor")
                    {
                        string s;
                        if (!(Root.FormDisplay.Visible || Root.FormCollection.Visible))
                        {
                            resp.StatusCode = 409;
                            ret = "!!!!! Not in Inking mode";
                        }
                        else if (query.TryGetValue("A", out s))
                        {
                            s = s.ToLower();
                            if (s == "true")
                                Root.APIRestAltPressed = true;
                            else if (s == "false")
                                Root.APIRestAltPressed = false;
                            else
                            {
                                resp.StatusCode = 400;
                                ret = string.Format("!!!! Error in Query ({0}) - {1} ", req.HttpMethod, req.Url.AbsoluteUri);
                            }
                            System.Windows.Forms.Cursor.Position = new Point(System.Windows.Forms.Cursor.Position.X, System.Windows.Forms.Cursor.Position.Y);
                        }
                        if (resp.StatusCode == 200)
                            ret = " { \"ArrowForced\" : " + (Root.APIRestAltPressed ? "true" : "false") + " }";
                    }


                    else if (req.Url.AbsolutePath == "/PickupColor")
                    {
                        string s;
                        if (!(Root.FormDisplay.Visible || Root.FormCollection.Visible))
                        {
                            resp.StatusCode = 409;
                            ret = "!!!!! Not in Inking mode";
                        }
                        else if (query.TryGetValue("P", out s))
                        {
                            s = s.ToLower();
                            if (s == "true")
                                Root.FormCollection.StartStopPickUpColor(1);
                            else if (s == "false")
                                Root.FormCollection.StartStopPickUpColor(0);
                            else
                            {
                                resp.StatusCode = 400;
                                ret = string.Format("!!!! Error in Query ({0}) - {1} ", req.HttpMethod, req.Url.AbsoluteUri);
                            }
                        }
                        if (resp.StatusCode == 200)
                            ret = " { \"PickupMode\" : "+(Root.ColorPickerMode ? "true" : "false") + 
                                  (!Root.ColorPickerMode?"}":string.Format(",\n\"Red\" : {0}, \"Green\" : {1}, \"Blue\" : {2}, \"Transparency\" : {3}  }}",
                                                                           Root.PickupColor.R, Root.PickupColor.G, Root.PickupColor.B, Root.PickupTransparency));
                    }


                    else // unknow command...
                    {
                        resp.StatusCode = 404;
                        ret = string.Format("!!!! unimplemented ({0}) - {1}", req.HttpMethod, req.Url.AbsoluteUri);
                    }

                }
                catch (Exception e)
                {
                    resp.StatusCode = 500;
                    ret = string.Format("!!!! Exception raised {0} ({1}) - {2} ", e.Message, req.HttpMethod, req.Url.AbsoluteUri);
                }
                if(resp.StatusCode==200)
                    Root.AppGetFocus(); // force focus

                // Write the response info
                byte[] data = Encoding.UTF8.GetBytes(ret);
                if (resp.StatusCode == 200)
                    resp.ContentType = "application/json";
                else
                    resp.ContentType = "text/plain";
                resp.ContentEncoding = Encoding.UTF8;
                resp.ContentLength64 = data.LongLength;

                // Write out to the response stream (asynchronously), then close it
                await resp.OutputStream.WriteAsync(data, 0, data.Length);
                resp.Close();
            }
        }

    }
}