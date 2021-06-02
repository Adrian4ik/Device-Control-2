using System;
using System.IO;
using System.Windows.Forms;

namespace Device_Control_2.Features
{
    class Logs
    {
        string path;

        public Logs()
        {
            FileInfo fi = new FileInfo(Application.ExecutablePath);
            path = Application.ExecutablePath.Substring(0, Application.ExecutablePath.Length - fi.Name.Length);
        }

        public void Write(string description)
        {
            CheckLog();

            string date = DateTime.Now.Year.ToString();
            date += (DateTime.Now.Month < 10) ? "0" + DateTime.Now.Month : DateTime.Now.Month.ToString();
            date += (DateTime.Now.Day < 10) ? "0" + DateTime.Now.Day : DateTime.Now.Day.ToString();

            File.AppendAllText(path + "log\\" + date + ".txt", "[" + DateTime.Now + "] " + description + "\n");
        }

        public void Write(string name, string description)
        {
            CheckLog();

            string date = DateTime.Now.Year.ToString();
            date += (DateTime.Now.Month < 10) ? "0" + DateTime.Now.Month : DateTime.Now.Month.ToString();
            date += (DateTime.Now.Day < 10) ? "0" + DateTime.Now.Day : DateTime.Now.Day.ToString();

            File.AppendAllText(path + "log\\" + date + ".txt", "[" + DateTime.Now + "] <" + name + "> " + description + "\n");
        }

        private void CheckLog()
        {
            string date = DateTime.Now.Year.ToString();
            date += (DateTime.Now.Month < 10) ? "0" + DateTime.Now.Month : DateTime.Now.Month.ToString();
            date += (DateTime.Now.Day < 10) ? "0" + DateTime.Now.Day : DateTime.Now.Day.ToString();

            if (!Directory.Exists(path + "log"))
            {
                Directory.CreateDirectory(path + "log");

                FileStream f = File.Create(path + "log\\" + date + ".txt");
                f.Close();
            }
            else if (!File.Exists(path + "log\\" + date + ".txt"))
            {
                FileStream f = File.Create(path + "log\\" + date + ".txt");
                f.Close();
            }
        }

        public void WriteEvent(string description)
        {
            Write(description);

            CheckEventLog();

            string date = DateTime.Now.Year.ToString();
            date += (DateTime.Now.Month < 10) ? "0" + DateTime.Now.Month : DateTime.Now.Month.ToString();

            File.AppendAllText(path + "event log\\" + date + ".txt", "[" + DateTime.Now + "] " + description + "\n");
        }

        public void WriteEvent(string name, string description)
        {
            Write(name, description);

            CheckEventLog();

            string date = DateTime.Now.Year.ToString();
            date += (DateTime.Now.Month < 10) ? "0" + DateTime.Now.Month : DateTime.Now.Month.ToString();

            File.AppendAllText(path + "event log\\" + date + ".txt", "[" + DateTime.Now + "] <" + name + "> " + description + "\n");
        }

        private void CheckEventLog()
        {
            string date = DateTime.Now.Year.ToString();
            date += (DateTime.Now.Month < 10) ? "0" + DateTime.Now.Month : DateTime.Now.Month.ToString();

            if (!Directory.Exists(path + "event log"))
            {
                Directory.CreateDirectory(path + "event log");

                FileStream f = File.Create(path + "event log\\" + date + ".txt");
                f.Close();
            }
            else if (!File.Exists(path + "event log\\" + date + ".txt"))
            {
                FileStream f = File.Create(path + "event log\\" + date + ".txt");
                f.Close();
            }
        }
    }
}
