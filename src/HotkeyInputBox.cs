﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace gInk
{
    public partial class HotkeyInputBox : TextBox
    {

        private Hotkey _Hotkey = new Hotkey();
        private bool _ExternalConflictFlag;
        private bool _InWaitingKey;

        public event EventHandler OnHotkeyChanged;

        public Hotkey Hotkey
        {
            get
            {
                return _Hotkey;
            }
            set
            {
                _Hotkey = value;
                UpdateText();
                if (OnHotkeyChanged != null)
                    OnHotkeyChanged(this, null);
            }
        }

        public bool RequireModifier { get; set; }

        public bool ExternalConflictFlag
        {
            get
            {
                return _ExternalConflictFlag;
            }
            set
            {
                _ExternalConflictFlag = value;
                SetBackColor();
            }
        }

        private bool HotkeyJustSet = false;

        public HotkeyInputBox()
        {
            InitializeComponent();

            Width = 150;
        }

        public void UpdateText()
        {
            Text = Hotkey.ToString();
        }

        protected void SetBackColor()
        {
            if (_InWaitingKey)
                BackColor = Color.LimeGreen;
            else if (_ExternalConflictFlag)
                BackColor = Color.IndianRed;
            else
                BackColor = Color.White;
        }


        protected override void OnKeyDown(KeyEventArgs e)
        {
            e.SuppressKeyPress = true;

            Keys modifierKeys = e.Modifiers;
            Keys pressedKey = e.KeyData ^ modifierKeys;

            //bool deleting = pressedKey == Keys.Escape || pressedKey == Keys.Delete || pressedKey == Keys.Back;
            bool deleting = pressedKey == Keys.Delete || pressedKey == Keys.Back;
            if (deleting)
            {
                Text = LocalSt.KeyNames[0];
            }
            else
            {
                Text = "";
                if ((modifierKeys & Keys.Control) > 0)
                    Text += LocalSt.KeyNames[0x00020000] + " + ";
                if ((modifierKeys & Keys.Alt) > 0)
                    Text += LocalSt.KeyNames[0x00040000] + " + ";
                if ((modifierKeys & Keys.Shift) > 0)
                    Text += LocalSt.KeyNames[0x00010000] + " + ";
                if ((modifierKeys & Keys.LWin) > 0 || (modifierKeys & Keys.RWin) > 0)
                    Text += LocalSt.KeyNames[0x5B] + " + ";

                if (Hotkey.IsValidKey(pressedKey))
                {
                    Text += LocalSt.KeyNames[(int)pressedKey];
                }
            }

            if (deleting)
            {
                Hotkey.Key = 0;
                Hotkey.Control = false;
                Hotkey.Alt = false;
                Hotkey.Shift = false;
                Hotkey.Win = false;
                HotkeyJustSet = true;
                _InWaitingKey = false;
                SetBackColor();
                if (OnHotkeyChanged != null)
                    OnHotkeyChanged(this, null);
            }
            else if ((!RequireModifier || modifierKeys != Keys.None) && Hotkey.IsValidKey(pressedKey))
            {
                Hotkey.Key = (int)pressedKey;
                Hotkey.Control = (modifierKeys & Keys.Control) > 0;
                Hotkey.Alt = (modifierKeys & Keys.Alt) > 0;
                Hotkey.Shift = (modifierKeys & Keys.Shift) > 0;
                Hotkey.Win = (modifierKeys & Keys.LWin) > 0 || (modifierKeys & Keys.RWin) > 0;
                HotkeyJustSet = true;
                _InWaitingKey = false;
                SetBackColor();
                if (OnHotkeyChanged != null)
                    OnHotkeyChanged(this, null);
            }
            else
            {
                _InWaitingKey = true;
                SetBackColor();
            }
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            Keys modifierKeys = e.Modifiers;
            Keys pressedKey = e.KeyData ^ modifierKeys;

            if (modifierKeys != Keys.None && !HotkeyJustSet)
            {
                Text = "";
                if ((modifierKeys & Keys.Control) > 0)
                    Text += LocalSt.KeyNames[0x00020000] + " + ";
                if ((modifierKeys & Keys.Alt) > 0)
                    Text += LocalSt.KeyNames[0x00040000] + " + ";
                if ((modifierKeys & Keys.Shift) > 0)
                    Text += LocalSt.KeyNames[0x00010000] + " + ";
                if ((modifierKeys & Keys.LWin) > 0 || (modifierKeys & Keys.RWin) > 0)
                    Text += LocalSt.KeyNames[0x5B] + " + ";

                if (Hotkey.IsValidKey(pressedKey))
                {
                    Text += LocalSt.KeyNames[(int)pressedKey];
                }
            }

            if (modifierKeys == Keys.None)
            {
                UpdateText();
                _InWaitingKey = false;
                SetBackColor();
                HotkeyJustSet = false;
            }
        }

    }
}
