﻿#region Using directives
using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
#endregion

namespace treeDiM.Basics
{
    public partial class UCtrlOptInt : UserControl
    {
        #region Delegates
        public delegate void ValueChangedDelegate(object sender, EventArgs e);
        #endregion

        #region Events
        [Browsable(true)]
        public event ValueChangedDelegate ValueChanged;
        #endregion

        #region Constructor
        public UCtrlOptInt()
        {
            InitializeComponent();
            Value = OptInt.Zero;
        }
        #endregion

        #region Public properties
        [Browsable(true),
        EditorBrowsable(EditorBrowsableState.Always),
        DesignerSerializationVisibility(DesignerSerializationVisibility.Visible),
        Category("Appearance")]
        public override string Text
        {
            get { return chkbOpt.Text; }
            set { chkbOpt.Text = value; }
        }
        [Browsable(false),
        DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public OptInt Value
        {
            get { return new OptInt(chkbOpt.Checked, (int)nudInt.Value); }
            set
            {
                chkbOpt.Checked = value.Activated;
                try { nudInt.Value = value.Value; } catch (ArgumentOutOfRangeException) { }
                OnCheckChanged(this, null);
            }
        }
        [Browsable(true)]
        public int Minimum
        {
            get { return (int)nudInt.Minimum; }
            set { nudInt.Minimum = value; }
        }
        #endregion

        #region Event handlers
        private void OnCheckChanged(object sender, EventArgs e)
        {
            nudInt.Enabled = chkbOpt.Checked;
            ValueChanged?.Invoke(this, e);
        }
        private void OnValueChangedLocal(object sender, EventArgs e) => ValueChanged?.Invoke(this, e);
        private void OnSizeChanged(object sender, EventArgs e)
        {
            nudInt.Location = new Point(Width - stNudLength - stSpace, 0);
        }
        private void OnEnter(object sender, EventArgs e)
        {
            if (sender is NumericUpDown nud)
            {
                nud.Select(0, nud.Text.Length);
                if (MouseButtons == MouseButtons.Left)
                    selectByMouse = true;
            }
        }
        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (selectByMouse && sender is NumericUpDown nud)
            {
                nud.Select(0, nud.Text.Length);
                selectByMouse = false;
            }
        }
        #endregion

        #region Data member
        private bool selectByMouse = false;
        public static int stNudLength = 60;
        public static int stSpace = 38;
        #endregion
    }
}
