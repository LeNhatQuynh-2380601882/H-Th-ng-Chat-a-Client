using System;
using System.Windows.Forms;
using ChatDesktopApp.Forms;

namespace ChatDesktopApp
{
    static class Program
    {
        /// <summary>
        /// Entry point chính của ứng dụng Desktop Client.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new LoginForm());
        }
    }
}
