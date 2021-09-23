﻿using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using MessageLib;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Client
{
    public partial class Client : Form
    {
        private delegate void UiUpdate();
        private bool _isConnected;
        private string _myIp;
        private string _serverIp;
        private int _myPort;
        private int _serverPort;
        private Socket _socket;
        private Task _listenTask;
        private byte[] _fileToSend;

        public Client()
        {
            InitializeComponent();
            tbMessage.MaxLength = 32;

            UpdateFormItems();
        }

        private void Connect(object sender, EventArgs e)
        {
            try
            {
                (_myIp, _myPort) = EnsureServerAdressCorrect(tbMyAddress.Text);
                (_serverIp, _serverPort) = EnsureServerAdressCorrect(tbSendAddress.Text);

                _isConnected = !_isConnected;
                UpdateFormItems();

                if (_isConnected)
                {
                    _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    _listenTask = new Task(Listen);
                    _listenTask.Start();
                }
                else
                {
                    Close();
                    _listenTask.Dispose();
                }
            }
            catch(Exception exception)
            {
                lbChat.Items.Add(exception.Message);
            }
        }

        private (string ip, int port) EnsureServerAdressCorrect(string serverAdress)
        {
            if (Regex.IsMatch(serverAdress, @"^[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{0,3}:[0-9]{1,5}$"))
            {
                var address = serverAdress.Split(new string[] { ":" }, StringSplitOptions.None);
                return (address[0], int.Parse(address[1]));
            }
            throw new Exception("Неверный формат адресса");
        }

        private void UpdateFormItems()
        {
            tbMessage.Enabled = _isConnected;
            btSend.Enabled = _isConnected;
            btFile.Enabled = _isConnected;
            tbMyAddress.Enabled = !_isConnected;
            tbSendAddress.Enabled = !_isConnected;
            tbPath.Enabled = !_isConnected;
            btConnect.Text = _isConnected ? "Отключиться" : "Подключиться";
        }

        private void Listen()
        {
            try
            {
                IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Parse(_myIp), _myPort);
                _socket.Bind(ipEndPoint);

                while (true)
                {
                    StringBuilder str = new StringBuilder();
                    byte[] data = new byte[1024 * 8 * 7];
                    EndPoint senderEndPoint = new IPEndPoint(IPAddress.Parse(_serverIp), _serverPort);
                    do
                    {
                        var bytes = _socket.ReceiveFrom(data, ref senderEndPoint);
                        str.Append(Encoding.Unicode.GetString(data, 0, bytes));
                    }
                    while (_socket.Available > 0);

                    var senderIpEndPoint = (IPEndPoint)senderEndPoint;
                    JObject jsonMessage = (JObject)JsonConvert.DeserializeObject(str.ToString());

                    UiUpdate newMessage = () =>
                    {
                        lbChat.Items.Add($"{DateTime.Now.ToString("dd.MM HH:mm:ss")} | {senderIpEndPoint.Address}:{senderIpEndPoint.Port} ({jsonMessage?.Value<string>("Name")}) | {jsonMessage?.Value<string>("Message")} | {jsonMessage?.Value<string>("FileName")}");
                    };
                    this.Invoke(newMessage);
                }
            }
            catch (Exception exception)
            {
                UiUpdate errMessage = () =>
                {
                    lbChat.Items.Add(exception.Message);
                };
                this.Invoke(errMessage);
            }
            finally
            {
                Close();
            }
        }

        private void Send(object sender, EventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(tbMessage.Text))
                {
                    TextMessage message = new TextMessage()
                    {
                        Ip = _myIp,
                        Port = _myPort,
                        Name = $"Jhoe{_myPort}",
                        Message = tbMessage.Text,
                        FileName = lFileName.Text,
                        File = _fileToSend
                    };
                    string json = JsonConvert.SerializeObject(message);
                    byte[] data = Encoding.Unicode.GetBytes(json);
                    EndPoint receiverEndPoint = new IPEndPoint(IPAddress.Parse(_serverIp), _serverPort);
                    _socket.SendTo(data, receiverEndPoint);
                    UiUpdate sendMessage = () =>
                    {
                        tbMessage.Text = "";
                        lFileName.Text = "no file";
                        _fileToSend = Array.Empty<byte>();
                    };
                    this.Invoke(sendMessage);
                }
            }
            catch (Exception exception)
            {
                UiUpdate errMessage = () =>
                {
                    lbChat.Items.Add(exception.Message);
                };
                this.Invoke(errMessage);
            }
        }


        private void Close()
        {
            if (_socket != null)
            {
                _socket.Shutdown(SocketShutdown.Both);
                _socket.Close();
            }
        }

        private void SelectFile(object sender, EventArgs e)
        {
            if (openFileDialog.ShowDialog() == DialogResult.Cancel)
            {
                return;
            }
            string filename = openFileDialog.FileName;
            _fileToSend = System.IO.File.ReadAllBytes(filename);
            lFileName.Text = filename.Split('\\').Last();
        }
    }
}
