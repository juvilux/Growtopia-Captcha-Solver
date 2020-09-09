using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

using Tesseract;
using AForge.Imaging;
using static Growtopia_Captcha_Solver.Imports;

namespace Growtopia_Captcha_Solver
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (Process.GetProcessesByName("Growtopia").Length == 0)
            {
                MessageBox.Show("Growtopia is not running.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var proc = Process.GetProcessesByName("Growtopia")[0];
            if (CaptureWindow(proc.MainWindowHandle))
            {
                if (SolveCaptcha(proc))
                {
                    if (radioButton1.Checked)
                    {
                        SendMessage(proc.MainWindowHandle, 0x100, (int)Keys.Enter, 0);
                        Thread.Sleep(100);
                        SendMessage(proc.MainWindowHandle, 0x101, (int)Keys.Enter, 0);
                    }
                    else if (radioButton2.Checked)
                    {
                        if (File.Exists("submit1.txt"))
                        {
                            string[] coords = File.ReadAllText("submit1.txt").Split(':');
                            var button = new Point(int.Parse(coords[0]), int.Parse(coords[1]));
                            SendMessage(proc.MainWindowHandle, 0x201, 0x00000001, Coordinate(button));
                            Thread.Sleep(100);
                            SendMessage(proc.MainWindowHandle, 0x202, 0x00000001, Coordinate(button));
                        }
                        else
                        {
                            FindSubmitButton(proc);
                        }
                    }
                    else if (radioButton3.Checked)
                    {
                        if (File.Exists("submit2.txt"))
                        {
                            string[] coords = File.ReadAllText("submit2.txt").Split(':');
                            var button = new Point(int.Parse(coords[0]), int.Parse(coords[1]));
                            SendMessage(proc.MainWindowHandle, 0x201, 0x00000001, Coordinate(button));
                            Thread.Sleep(100);
                            SendMessage(proc.MainWindowHandle, 0x202, 0x00000001, Coordinate(button));
                        }
                        else
                        {
                            MessageBox.Show("Try to change the coordinates.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
                else
                {
                    MessageBox.Show("Neither not a captcha or failed to read.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                MessageBox.Show("Failed to capture screenshot.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void textBox1_MouseDown(object sender, MouseEventArgs e)
        {
            if (Process.GetProcessesByName("Growtopia").Length > 0)
            {
                Cursor.Current = Cursors.Cross;
                this.Opacity = 0.5;
            }
            else
            {
                MessageBox.Show("Growtopia is not running.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void textBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if (Process.GetProcessesByName("Growtopia").Length > 0 && Cursor.Current == Cursors.Cross)
            {
                RECT wrc;
                GetWindowRect(Process.GetProcessesByName("Growtopia")[0].MainWindowHandle, out wrc);

                RECT crc;
                GetClientRect(Process.GetProcessesByName("Growtopia")[0].MainWindowHandle, out crc);

                Point lefttop = new Point(crc.Left, crc.Top);
                ClientToScreen(Process.GetProcessesByName("Growtopia")[0].MainWindowHandle, out lefttop);

                Point rightbottom = new Point(crc.Right, crc.Bottom);
                ClientToScreen(Process.GetProcessesByName("Growtopia")[0].MainWindowHandle, out rightbottom);

                int wintop = lefttop.Y - wrc.Top;
                int winbottom = wrc.Bottom - rightbottom.Y;
                int winleft = lefttop.X - wrc.Left;
                int winright = wrc.Right - rightbottom.X;

                int winwidth = wrc.Right - wrc.Left - winleft - winright;
                int winheight = wrc.Bottom - wrc.Top - wintop - winbottom;

                int xpos = Cursor.Position.X - wrc.Left - winleft;
                int ypos = Cursor.Position.Y - wrc.Top - wintop;

                if (xpos >= 0 && ypos >= 0 && xpos <= winwidth && ypos <= winheight)
                {
                    textBox1.Text = string.Format("{0}:{1}", xpos, ypos);
                }
                else
                {
                    textBox1.Text = "0:0";
                }
            }
        }

        private void textBox1_MouseUp(object sender, MouseEventArgs e)
        {
            if (this.Opacity < 1 && Cursor.Current == Cursors.Cross)
            {
                Cursor.Current = Cursors.Default;
                this.Opacity = 1;
            }
            CreateTextFile("submit2.txt", textBox1.Text);
        }

        private bool CaptureWindow(IntPtr handle)
        {
            try
            {
                SetForegroundWindow(handle);
                ShowWindow(handle, SW_RESTORE);
                Thread.Sleep(1000);

                RECT rect = new RECT();
                GetWindowRect(handle, out rect);

                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;

                Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                using (Graphics graphics = Graphics.FromImage(bmp))
                {
                    graphics.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
                }
                bmp.Save(Application.StartupPath + "\\capture.jpg", System.Drawing.Imaging.ImageFormat.Jpeg);

                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool SolveCaptcha(Process proc)
        {
            try
            {
                using (var tesseract = new TesseractEngine(Application.StartupPath, "eng", EngineMode.Default))
                {
                    using (var image = Pix.LoadFromFile(Application.StartupPath + "\\capture.jpg"))
                    {
                        using (var read = tesseract.Process(image))
                        {
                            var input = read.GetText();

                            int ans = 0;
                            int counter = 0;
                            string[] split = Regex.Split(input, "(\r\n|\r|\n)");
                            foreach (var line in split)
                            {
                                richTextBox1.Text += string.Format("[{0}]: {1}{2}", counter++, line, Environment.NewLine);
                                Regex regex = new Regex(@"([+0-9])+(\+|\*|it|t|T)([+0-9])+");
                                Match match = regex.Match(line.Replace(" ", ""));
                                if (match.Success)
                                {
                                    var q = ChangePlus(match.Value);
                                    string[] nums = q.Split('+');
                                    foreach (var c in nums)
                                    {
                                        var num = ChangeNum(c);
                                        if (int.TryParse(num, out _))
                                        {
                                            ans += int.Parse(num);
                                        }
                                    }
                                }
                            }

                            if (ans == 0)
                            {
                                return false;
                            }
                            else
                            {
                                char[] final = ans.ToString().ToCharArray();
                                foreach (char c in final)
                                {
                                    SendMessage(proc.MainWindowHandle, 0x100, c, 0);
                                    Thread.Sleep(50);
                                    SendMessage(proc.MainWindowHandle, 0x101, c, 0);
                                }
                                return true;
                            }
                        }
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        private void FindSubmitButton(Process proc)
        {
            // https://stackoverflow.com/questions/2472467/how-to-find-one-image-inside-of-another

            try
            {
                Bitmap sourceImage = (Bitmap)Bitmap.FromFile(Application.StartupPath + "\\capture.jpg");
                Bitmap baseImage = (Bitmap)Bitmap.FromFile(Application.StartupPath + "\\submit.jpg");

                ExhaustiveTemplateMatching tm = new ExhaustiveTemplateMatching(0.921f);
                // find all matchings with specified above similarity

                TemplateMatch[] matchings = tm.ProcessImage(sourceImage, baseImage);
                // highlight found matchings

                BitmapData data = sourceImage.LockBits(new Rectangle(0, 0, sourceImage.Width, sourceImage.Height), ImageLockMode.ReadOnly, sourceImage.PixelFormat);
                sourceImage.UnlockBits(data);
                sourceImage.Dispose();
                baseImage.Dispose();

                var r = matchings[0].Rectangle;

                SendMessage(proc.MainWindowHandle, 0x201, 0x00000001, Coordinate(r.X, r.Y));
                Thread.Sleep(100);
                SendMessage(proc.MainWindowHandle, 0x202, 0x00000001, Coordinate(r.X, r.Y));

                CreateTextFile("submit1.txt", string.Format("{0}:{1}", r.X.ToString(), r.Y.ToString()));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        // Misc
        private string ChangePlus(string input)
        {
            string sym = input;
            sym = Regex.Replace(sym, "(it|t|T|\\*)", "+");
            return sym;
        }

        private string ChangeNum(string input)
        {
            // Check for other numbers.
            string num = input;
            num = Regex.Replace(num, "( |  *)", "");
            num = Regex.Replace(num, "(o|O)", "0");
            num = Regex.Replace(num, "(i|l|I)", "1");
            return num;
        }

        private void CreateTextFile(string filename, string content)
        {
            using (StreamWriter writer = new StreamWriter(filename, false))
            {
                writer.Write(content);
                writer.Flush();
                writer.Close();
            }
        }
    }
}
