using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using Open.Nat;

namespace ShareStuff
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        int myPort = 5010;
        bool keepGoing = true;
        string incomingFileName = "";
        double incomingFileSize = 0;
        DateTime startTime = DateTime.Now;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void btnSendText_Click(object sender, RoutedEventArgs e)
        {
            SendChatMessage(txtChatSend.Text);
        }

        private bool SendChatMessage(string message)
        {
            string messageToSend = message;
            string targetIP = "";
            int targetPort = 0;
            bool weAreOk = true;
            txtChatSend.Dispatcher.Invoke(() =>
            {
                int tempPort = 5010;
                targetIP = txtTargetIP.Text.Trim();
                int.TryParse(txtTargetPort.Text.Trim(), out tempPort);
                targetPort = tempPort;
                if (targetIP.Length == 0)
                {
                    MessageBox.Show("You must enter a target IP");
                    weAreOk = false;
                }
            });
            if (!weAreOk)
                return false;

            Task.Factory.StartNew(() =>
            {
                using (var client = new TcpClient(targetIP, targetPort))
                {
                    if (client.Connected)
                    {
                        using (StreamWriter writer = new StreamWriter(client.GetStream()))
                        {
                            writer.WriteLine(messageToSend);
                            writer.Flush();
                        }
                    }
                    else
                    {
                        txtChatSend.Dispatcher.Invoke(() =>
                        {
                            txtChatSend.Text = "";
                            lblChatRecieved.Text += "Last Message Not Recieved!";
                        });
                    }
                    client.Close();
                }
            });
            txtChatSend.Dispatcher.Invoke(() =>
            {
                txtChatSend.Text = "";
                lblChatRecieved.Text += "[Me - " + DateTime.Now.ToShortTimeString() + "] - " + message + "\r\n";
                chatScrollViewer.ScrollToBottom();
            });
            return true;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                try
                {
                    //var discoverer = new NatDiscoverer();
                    //var cts = new CancellationTokenSource(10000);
                    //var device = await discoverer.DiscoverDeviceAsync(PortMapper.Upnp, cts);
                    //var externTest = await device.GetExternalIPAsync();
                    //await device.DeletePortMapAsync(new Mapping(Protocol.Tcp, myPort, myPort, "ShareStuffChat"));
                    //await device.DeletePortMapAsync(new Mapping(Protocol.Tcp, (myPort + 1), (myPort + 1), "ShareStuffFile"));
                    //await device.CreatePortMapAsync(new Mapping(Protocol.Tcp, myPort, myPort, "ShareStuffChat"));
                    //await device.CreatePortMapAsync(new Mapping(Protocol.Tcp, (myPort + 1), (myPort + 1), "ShareStuffFile"));
                    //string foo = externTest.ToString();
                    //lblMyIPAndPort.Text = "Your IP Is: " + foo + "  Your Port Is: " + myPort;
                }
                catch (Exception)
                {
                    lblMyIPAndPort.Text = "Could Not Auto Configure or Get IP";
                }
                Progress<string> progStatusBar = new Progress<string>();
                progStatusBar.ProgressChanged += (a, b) =>
                {
                    lblStatus.Text = b;
                };
                Progress<double> progUpdateBar = new Progress<double>();
                progUpdateBar.ProgressChanged += (a, b) =>
                {
                    progBar.Value = b;
                };

                Task.Factory.StartNew(() => Listen());
                Task.Factory.StartNew(() => Listen2(progStatusBar, progUpdateBar));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private static string GetPublicIpAddress()
        {
            using (WebClient webClient = new WebClient())
            {
                string publicIPAddress = webClient.DownloadString("http://bot.whatismyipaddress.com");
                return publicIPAddress.Replace("\n", "");
            }
        }

        private void Listen()
        {
            string message = "";
            var listener = new TcpListener(myPort);
            listener.Start();
            do
            {
                using (TcpClient client = listener.AcceptTcpClient())
                {
                    using (StreamReader objReader = new StreamReader(client.GetStream()))
                    {
                        message = objReader.ReadLine() + "\r\n";
                    }
                    client.Close();
                }

                Regex reg = new Regex(@".+\*(?<name>.+?=*)\*(?<size>.+)\*");
                if (reg.IsMatch(message))
                {
                    string fileName = reg.Match(message).Groups["name"].Value;
                    double fileSize = double.Parse(reg.Match(message).Groups["size"].Value);
                    incomingFileName = fileName;
                    incomingFileSize = fileSize;
                }
                lblChatRecieved.Dispatcher.Invoke(() =>
                {
                    lblChatRecieved.Text += "[Them - " + DateTime.Now.ToShortTimeString() + "] - " + message;
                    chatScrollViewer.ScrollToBottom();
                });
            } while (keepGoing);
        }

        private void Listen2(Progress<string> progStatusBar, Progress<double> progUpdateBar)
        {
            var listener = new TcpListener((myPort + 1));
            listener.Start();
            IProgress<string> proUpdateStatus = progStatusBar;
            IProgress<double> proUpdateBar = progUpdateBar;
            do
            {
                TcpClient client = listener.AcceptTcpClient();
                using (var netStream = client.GetStream())
                {
                    Thread.Sleep(60);
                    SaveFileDialog sfd = new SaveFileDialog();
                    sfd.RestoreDirectory = true;
                    sfd.FileName = incomingFileName;
                    sfd.Title = "Where do you want to save the file?";
                    sfd.ShowDialog();
                    string saveFilename = sfd.FileName;
                    long totalRecievedBytes = 0;
                    byte[] recBuffer = new byte[81920];
                    int recievedBytes;
                    startTime = DateTime.Now;
                    lblChatRecieved.Dispatcher.Invoke(() =>
                    {
                        progBar.Maximum = (incomingFileSize + 1);
                    });
                    using (FileStream fs = new FileStream(saveFilename, FileMode.Create, FileAccess.Write))
                    {
                        //CopyStream(netStream, fs, prog2, prog);
                        while ((recievedBytes = netStream.Read(recBuffer, 0, recBuffer.Length)) != 0)
                        {
                            fs.Write(recBuffer, 0, recievedBytes);
                            totalRecievedBytes += recievedBytes;
                            proUpdateBar.Report(totalRecievedBytes);
                            long totalSeconds = (long)(DateTime.Now - startTime).TotalSeconds;
                            long itemsPerSecond = totalRecievedBytes / (totalSeconds == 0 ? 1 : totalSeconds);
                            long secondsRemaining = ((long)incomingFileSize - totalRecievedBytes) / (itemsPerSecond == 0 ? 1 : itemsPerSecond);
                            proUpdateStatus.Report("Processing at " + (itemsPerSecond / 1024) + " KB/s - Remaining Time: " + FormatDurationSeconds(secondsRemaining));
                        }
                    }
                }
                lblChatRecieved.Dispatcher.Invoke(() =>
                {
                    lblStatus.Text = "Download Complete!";
                });
            } while (keepGoing);
        }

        private void btnSendFile_Click(object sender, RoutedEventArgs e)
        {
            lblStatus.Text = "Sending File...";
            string fileToSend = "";
            var ofd = new System.Windows.Forms.OpenFileDialog();
            ofd.Multiselect = false;
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                fileToSend = ofd.FileName;
                FileInfo fi = new FileInfo(fileToSend);
                var message = "User is trying to send *" + ofd.SafeFileName.Replace('*', '-') + "*" + fi.Length + "*";
                if (!SendChatMessage(message))
                    return;
                int targetPort = (int.Parse(txtTargetPort.Text) + 1);
                string targetIP = txtTargetIP.Text;

                Progress<string> prog2 = new Progress<string>();
                prog2.ProgressChanged += (a, b) => { lblStatus.Text = b; };

                Progress<double> prog = new Progress<double>();
                prog.ProgressChanged += (a, b) => { progBar.Value = b; };
                btnSendFile.IsEnabled = false;

                Task.Factory.StartNew(() =>
                {
                    IProgress<double> pro = prog;
                    IProgress<string> pro2 = prog2;
                    byte[] sendingBuffer = null;
                    int bufferSize = 81920;
                    using (TcpClient client = new TcpClient(targetIP, targetPort))
                    {
                        using (NetworkStream netStream = client.GetStream())
                        {
                            using (FileStream fs = new FileStream(fileToSend, FileMode.Open, FileAccess.Read))
                            {
                                //CopyStream(fs, netStream, prog2, prog);
                                int numberOfPackets = Convert.ToInt32((Math.Ceiling(Convert.ToDouble(fs.Length) / Convert.ToDouble(bufferSize))));
                                lblChatRecieved.Dispatcher.Invoke(() =>
                                {
                                    progBar.Value = 0;
                                    progBar.Maximum = (numberOfPackets + 3);
                                });
                                long totalLength = fs.Length;
                                int currentPacketLength;
                                double counter = 0;
                                startTime = DateTime.Now;
                                for (int i = 0; i < numberOfPackets; i++)
                                {
                                    if (totalLength >= bufferSize)
                                    {
                                        currentPacketLength = bufferSize;
                                        totalLength = totalLength - currentPacketLength;
                                    }
                                    else
                                    {
                                        currentPacketLength = (int)totalLength;
                                    }
                                    sendingBuffer = new byte[currentPacketLength];
                                    fs.Read(sendingBuffer, 0, currentPacketLength);
                                    netStream.Write(sendingBuffer, 0, sendingBuffer.Length);
                                    counter++;
                                    pro.Report(counter);
                                    long totalSeconds = (long)(DateTime.Now - startTime).TotalSeconds;
                                    long itemsPerSecond = (int)counter / (totalSeconds == 0 ? 1 : totalSeconds);
                                    long secondsRemaining = (numberOfPackets - (int)counter) / (itemsPerSecond == 0 ? 1 : itemsPerSecond);
                                    pro2.Report("Processing at " + ((itemsPerSecond * bufferSize) / 1024) + " KB/s - Remaining Time: " + FormatDurationSeconds(secondsRemaining));
                                }
                            }
                        }
                    }
                }).ContinueWith(a =>
                {
                    lblStatus.Text = "Transfer complete.";
                    btnSendFile.IsEnabled = true;
                }, TaskScheduler.FromCurrentSynchronizationContext());
            }
        }
        
        private void txtChatSend_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.Enter)
            {
                return;
            }
            e.Handled = true;
            SendChatMessage(txtChatSend.Text);
        }

        public static string FormatDurationSeconds(long seconds)
        {
            var duration = TimeSpan.FromSeconds(seconds);
            string result = "";
            result += String.Format("{0:%m} min, {0:%s} sec", duration);
            return result;
        }
    }
}
