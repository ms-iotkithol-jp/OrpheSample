using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;

namespace OrpheTestApp
{
    public class AzureIoTHubBroker
    {
        Microsoft.Azure.Devices.Client.DeviceClient deviceClient;
        string connectionString = "";
        public AzureIoTHubBroker(string cs)
        {
            connectionString = cs;
            IsConnected = false;
        }

        public bool IsConnected { get; set; }

        public async Task ConnectAsync()
        {
            if (deviceClient == null)
            {
                deviceClient = Microsoft.Azure.Devices.Client.DeviceClient.CreateFromConnectionString(connectionString, Microsoft.Azure.Devices.Client.TransportType.Amqp);
            }
            await deviceClient.OpenAsync();
            ReceiveMessages();
            IsConnected = true;
        }

        public async Task CloseAsync()
        {
            if (deviceClient!=null)
            {
                toContinue = false;
                await deviceClient.CloseAsync();
                IsConnected = false;
            }
        }

        List<SensorData> sendData = new List<SensorData>();
        DispatcherTimer sendTimer;

        public void Start(int intervalMS)
        {
            if (sendTimer == null)
            {
                sendTimer = new DispatcherTimer();
                sendTimer.Tick += SendTimer_Tick;
            }
            sendTimer.Interval = TimeSpan.FromMilliseconds(intervalMS);
            sendTimer.Start();
        }

        public void Stop()
        {
            if (sendTimer != null)
            {
                sendTimer.Stop();
            }
        }

        public void Send(string side, double ax, double ay, double az,DateTime ts)
        {
            lock (this)
            {
                sendData.Add(new SensorData()
                {
                    Side = side,
                    AccelX = ax,
                    AccelY = ay,
                    AccelZ = az,
                    MeasuredTime = ts
                });
            }
        }
        private  async void SendTimer_Tick(object sender, object e)
        {
            if (deviceClient == null || sendData.Count == 0)
                return;

            string content = "";
            lock(this)
            {
                content = Newtonsoft.Json.JsonConvert.SerializeObject(sendData);
                sendData.Clear();
            }
            var msg = new Microsoft.Azure.Devices.Client.Message(Encoding.UTF8.GetBytes(content));
            await deviceClient.SendEventAsync(msg);
        }

        bool toContinue = true;
        async Task ReceiveMessages()
        {
            while(toContinue)
            {
                var msg = await deviceClient.ReceiveAsync();
                if (OnC2DMessageReceived != null)
                {
                    OnC2DMessageReceived(this, Encoding.UTF8.GetString(msg.GetBytes()));
                }
            }
        }

        public delegate void C2DMessageReceivedHandler(object src, string message);
        public event C2DMessageReceivedHandler OnC2DMessageReceived;

    }

    public class SensorData
    {
        public string Side { get; set; }
        public DateTime MeasuredTime { get; set; }
        public double AccelX { get; set; }
        public double AccelY { get; set; }
        public double AccelZ { get; set; }
    }
}
