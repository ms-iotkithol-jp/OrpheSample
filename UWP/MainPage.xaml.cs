using Orphe;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using Windows.Devices.Enumeration;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace OrpheTestApp
{
    public sealed partial class MainPage : Page
    {
        private OrpheShoe _OrpheShoe = null;

        public MainPage()
        {
            this.InitializeComponent();
        }


        private DeviceWatcher _DeviceWatcher;
        private ObservableCollection<string> _DeviceIdList = new ObservableCollection<string>();

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            _DeviceWatcher = DeviceInformation.CreateWatcher(OrpheShoe.GetDeviceSelector(), new string[] { "System.Devices.Aep.IsConnected", "System.Devices.Aep.SignalStrength", }, DeviceInformationKind.AssociationEndpoint);
            _DeviceWatcher.Added += DeviceWatcher_Added;
            _DeviceWatcher.Updated += DeviceWatcher_Updated;
            _DeviceWatcher.Removed += DeviceWatcher_Removed;
            _DeviceWatcher.Start();
        }

        private async void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation args)
        {
            Debug.WriteLine("Added.");
            foreach (var key in args.Properties.Keys) Debug.WriteLine($" {key}={args.Properties[key]}");

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                _DeviceIdList.Add(args.Id);
            });
        }

        private void DeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            Debug.WriteLine("Updated.");
            foreach (var key in args.Properties.Keys) Debug.WriteLine($" {key}={args.Properties[key]}");
        }

        private async void DeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                _DeviceIdList.Remove(args.Id);
            });
        }

        private async void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            var cursor = Window.Current.CoreWindow.PointerCursor;
            Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Wait, cursor.Id);
            try
            {
                if (lstDeviceIdList.SelectedIndex < 0) return;

                _OrpheShoe = new OrpheShoe();
                _OrpheShoe.ValueChanged += OrpheShoe_ValueChanged;
                if (!await _OrpheShoe.Connect((string)lstDeviceIdList.SelectedItem))
                {
                    _OrpheShoe = null;
                    await new MessageDialog("Orpheとの接続確立に失敗しました。\nOrpheをペアリングモードに変更して再接続してください。").ShowAsync();
                    return;
                }

                _DeviceWatcher.Stop();

                lstDeviceIdList.IsEnabled = false;
                btnConnect.IsEnabled = false;
                btnScene.IsEnabled = true;
                btnLightTrigger.IsEnabled = true;
                btnLightOn.IsEnabled = true;
                btnLightOff.IsEnabled = true;
            }
            finally
            {
                Window.Current.CoreWindow.PointerCursor = cursor;
            }
        }

        private async void btnScene_Click(object sender, RoutedEventArgs e)
        {
            await _OrpheShoe.SetScene(int.Parse(txtScene.Text));
        }

        private async void btnLightTrigger_Click(object sender, RoutedEventArgs e)
        {
            await _OrpheShoe.TriggerLight(int.Parse(txtLightNum.Text));
        }

        private async void btnLightOn_Click(object sender, RoutedEventArgs e)
        {
            await _OrpheShoe.SwitchLight(int.Parse(txtLightNum.Text), true);
        }

        private async void btnLightOff_Click(object sender, RoutedEventArgs e)
        {
            await _OrpheShoe.SwitchLight(int.Parse(txtLightNum.Text), false);
        }

        private void OrpheShoe_ValueChanged(object sender, OrpheValueChangedEventArgs e)
        {
            var now = DateTime.Now;
            Debug.WriteLine("{0:HHmmssfff} {1:f3} {2:f3} {3:f3}", now, e.Acceleration.x, e.Acceleration.y, e.Acceleration.z);

            if (iotHubBroker != null && iotHubBroker.IsConnected)
            {
                iotHubBroker.Send("", e.Acceleration.x, e.Acceleration.y, e.Acceleration.z, e.Gyro.x,e.Gyro.y,e.Gyro.z, e.Compass,e.Quaternion.w,e.Quaternion.x,e.Quaternion.y,e.Quaternion.z,e.Shock, now);
            }
        }

        private AzureIoTHubBroker iotHubBroker;

        private async void buttonAzureIoTHubConn_Click(System.Object sender, RoutedEventArgs e)
        {
            if (iotHubBroker == null)
            {
                if (string.IsNullOrEmpty(tbIoTHubCS.Text))
                {
                    var msgDialog =new MessageDialog("Please set IoT Hub Device Connection String!");
                    await msgDialog.ShowAsync();
                    return;
                }
                iotHubBroker = new AzureIoTHubBroker(tbIoTHubCS.Text);
                iotHubBroker.OnC2DMessageReceived += IotHubBroker_OnC2DMessageReceived;
            }
            switch (buttonAzureIoTHubConn.Content.ToString())
            {
                case "Connect":
                    await iotHubBroker.ConnectAsync();
                    buttonAzureIoTHubConn.Content = "Start";
                    break;
                case "Start":
                     iotHubBroker.Start(int.Parse(tbIoTSendIntervalMS.Text));
                    buttonAzureIoTHubConn.Content = "Stop";
                    break;
                case "Stop":
                    iotHubBroker.Stop();
                    buttonAzureIoTHubConn.Content = "Disconnect";
                    break;
                case "Disconnect":
                    await iotHubBroker.CloseAsync();
                    buttonAzureIoTHubConn.Content = "Connect";
                    break;
            }
        }

        private void IotHubBroker_OnC2DMessageReceived(object src, string message)
        {
            var sb = new StringBuilder(tbAzureIoTLog.Text);
            sb.AppendLine(message);
            tbAzureIoTLog.Text = sb.ToString();
        }
    }
}
