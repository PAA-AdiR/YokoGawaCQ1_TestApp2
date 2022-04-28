using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yokogawa.LS.ICM.IRobotService;

namespace YokogawaCQ1_TestApp2
{
    public partial class Form1 : Form
    {
        #region Declarations
        TestClient TestClient = new TestClient();

        /// <summary>
        /// Service Client
        /// </summary>
        private ServiceClient<ICommandService> client { get { return TestClient.client; } }

        #endregion
        #region Properties
        /// <summary>
        /// Lock id
        /// </summary>
        private string lockID
        {
            get { return TestClient.lockID; }
            set { TestClient.lockID = value; }
        }
        /// <summary>
        /// Device ID
        /// </summary>
        private string deviceID
        {
            get { return TestClient.deviceID; }
            set { TestClient.deviceID = value; }
        }

        /// <summary>
        /// product management system ID
        /// </summary>
        private string pmsID
        {
            get { return TestClient.pmsID; }
            set { TestClient.pmsID = value; }
        }

        bool isSimuration
        {
            get
            {
                return TestClient.isSimuration;
            }
            set { TestClient.isSimuration = value; }
        }

        /// <summary>
        /// URI of DeviceControlEvent
        /// </summary>
        string eventURI { get { return TestClient.eventURI; } }
        #endregion

        public Form1()
        {
            InitializeComponent();
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            TestClient.clientURI = "http://localhost:50001/ICM/DeviceControl";
            TestClient.eventURI = "http://localhost:50001/ICM/DeviceControl/Event";

            TestClient.CreateClient();
            TestClient.CreateEventReceiverHost();
        }

        private void btnDisconnect_Click(object sender, EventArgs e)
        {
            TestClient.DisConnect();
        }



        /// <summary>
        /// Timeout until an errorhandling state is changed into an error state.
        /// </summary>
        private string errorHandlingTimeout
        {
            get
            {
                return TestClient.errorHandlingTimeout;
            }
        }

        /// <summary>
        /// If this parameter is omitted, no timeout will be set. Otherwise
        /// the device will unlock itself if it does not receive any commands within the timeout period.
        /// </summary>
        private string lockTimeout
        {
            get
            {
                return TestClient.lockTimeout;
            }
        }

        private async void btnPrepare_Click(object sender, EventArgs e)
        {
            try
            {
                isSimuration = false;

                lockID = Guid.NewGuid().ToString();
                deviceID = Guid.NewGuid().ToString();
                pmsID = Guid.NewGuid().ToString();
                await Task.Run(() => DoAction("Reset", (id) => client.Proxy.Reset(id, lockID, deviceID, eventURI, pmsID, errorHandlingTimeout, false), State.Standby));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        /// <summary>
        /// Execute Method
        /// </summary>
        /// <param name="act">method</param>
        /// <returns>be a timeout or nut</returns>
        bool DoAction(string commandName, Func<int, SiLAReturnValue> act, State status = State.Idle, TimeSpan? timeout = null, bool addLog = true)
        {
            var result = TestClient.DoAction(commandName, act, status, null, timeout, addLog);
            return result;
        }

        private void btnGetStatus_Click(object sender, EventArgs e)
        {
            var status = TestClient.GetStatus(out var deviceLocked);
        }
    }
}
