using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Core.BoxHelper
{
    public partial class LoginForm : Form
    {
        public LoginForm(string url)
        {
            InitializeComponent();

            webBrowser.Url = new Uri(url);
        }
    }
}
