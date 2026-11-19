using System;
using System.Threading;
using System.Windows.Forms;

namespace DriverGuardPrototype
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            using Mutex mutex = new Mutex(true, "Global\\DriverGuardPro_SingleInstance_Mutex", out bool isNewInstance);
            if (!isNewInstance)
            {
                MessageBox.Show("Driver Guard Pro is already running in the system tray.", "Driver Guard Pro", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}