using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NXMC_CameraResolutionCustomizer
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        public void BeginListUpdate()
        {
            this.listBoxResolution.BeginUpdate();
            this.listBoxResolution.Items.Clear();
        }

        public void AddItem(string name)
        {
            this.listBoxResolution.Items.Add(name);
        }

        public void EndListUpdate()
        {
            this.listBoxResolution.EndUpdate();
        }

        public int CurrentSelectedItemIndex()
        {
            return this.listBoxResolution.SelectedIndex;
        }
    }
}
