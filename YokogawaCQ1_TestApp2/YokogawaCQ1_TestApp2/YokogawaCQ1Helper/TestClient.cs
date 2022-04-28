using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using System.Threading;
using System.Threading.Tasks;
using Yokogawa.LS.ICM.IRobotService;
namespace YokogawaCQ1_TestApp2
{
    public class TestClient
    {
        /// <summary>
        /// Lock id
        /// </summary>
        public string lockID;
        /// <summary>
        /// Device ID
        /// </summary>
        public string deviceID;
        /// <summary>
        /// product management system ID
        /// </summary>
        public string pmsID;

        public bool isSimuration = true;

        public EventReceiverService evObj;

        /// <summary>
        /// Service Client
        /// </summary>
        public ServiceClient<ICommandService> client;

        /// <summary>
        /// Serivice Host
        /// </summary>
        private ServiceHost host;

        //string EVENTRECEIVERHOST_CRL_SERIAL = "8c5ed69069e8aa88429d18012f02de7d";

        /// <summary>
        /// Timeout until an errorhandling state is changed into an error state.
        /// </summary>
        //public string errorHandlingTimeout;

        /// <summary>
        /// request id
        /// </summary>
        private int counter = 1;

        static ConfirmCertificatePolicy policy = new ConfirmCertificatePolicy();
        /// <summary>
        /// Timeout until an errorhandling state is changed into an error state.
        /// </summary>
        public readonly string errorHandlingTimeout = Util.ConvertToDuraionString(TimeSpan.FromSeconds(10));

        /// <summary>
        /// If this parameter is omitted, no timeout will be set. Otherwise
        /// the device will unlock itself if it does not receive any commands within the timeout period.
        /// </summary>
        public readonly string lockTimeout = Util.ConvertToDuraionString(TimeSpan.FromMinutes(10));



        /// <summary>
        /// URI of DeviceControl
        /// </summary>
        public string clientURI = "http://localhost:50001/ICM/DeviceControl";

        /// <summary>
        /// adding log action
        /// </summary>
        public Action<int, string, string, string, bool> addLog = (id, command, title, message, isAddlog) => { };
        public Action<int, string, ResponseData, bool> addLog_Response = (id, command, response, isAddlog) => { };

        /// <summary>
        /// Create client
        /// </summary>
        public void CreateClient()
        {
            //policy.Serial = EVENTRECEIVERHOST_CRL_SERIAL;
            //Specify the binding to be used for the client.
            var binding = new BasicHttpBinding();
            //Specify the address to be used for the client.
            EndpointAddress address = new EndpointAddress(clientURI);

            client = new ServiceClient<ICommandService>(binding, address);
            client.Endpoint.EndpointBehaviors.Add(new InspectorBehavior());
        }

        public void DisConnect()
        {
            if (evObj != null)
            {
                evObj.OccurredResponseEvent -= evObj_OccurredResponse;
                evObj.OccurredError -= evObj_OccurredError;
            }

            if (client != null)
            {
                ((IDisposable)client).Dispose();
            }

            if (host != null)
            {
                ((IDisposable)host).Dispose();
            }
        }

        /// <summary>
        /// URI of DeviceControlEvent
        /// </summary>
        public string eventURI = "http://localhost:50001/ICM/DeviceControl/Event";

        /// <summary>
        /// Create host of event receiver
        /// </summary>
        public void CreateEventReceiverHost()
        {
            evObj = new EventReceiverService();
            evObj.OccurredResponseEvent += evObj_OccurredResponse;
            evObj.OccurredError += evObj_OccurredError;

            host = new ServiceHost(evObj);
            host.AddServiceEndpoint(
                typeof(IEventService),
                new BasicHttpBinding(),
                new Uri(eventURI));
            var behavior = host.Description.Behaviors.Find<ServiceBehaviorAttribute>();
            behavior.InstanceContextMode = InstanceContextMode.Single;
            host.Open();
        }

        /// <summary>
        /// return id
        /// </summary>
        /// <returns></returns>
        public int GetId()
        {
            var res = counter;
            System.Threading.Interlocked.Increment(ref counter);
            return res;
        }

        /// <summary>
        /// Execute Method
        /// </summary>
        /// <param name="act">method</param>
        /// <returns>be a timeout or nut</returns>
        public bool DoAction(string command, Func<int, SiLAReturnValue> act, State status = State.Idle, int? id = null, TimeSpan? timeout = null, bool isAddlog = true)
        {
            if (id == null)
            {
                id = GetId();
            }

            timeout = timeout ?? TimeSpan.FromMilliseconds(180000);

            bool isError = true;

            //addLog(id.Value, command, command + " (send)", null, isAddlog);
            System.Diagnostics.Debug.WriteLine("DoAction ID:" + id.Value);

            var r = act(id.Value);
            var returnEnum = (ReturnCodes)Enum.ToObject(typeof(ReturnCodes), r.returnCode);
            addLog(id.Value, command, command, returnEnum + " (" + r.returnCode + "), message:" + r.message, isAddlog);

            if (r.returnCode != (int)ReturnCodes.AsyncAccepted)
            {
                //addLog(id.Value, command, command + " (recieve)", returnEnum + " (" + r.returnCode + "), message:" + r.message, isAddlog);
                System.Diagnostics.Debug.WriteLine("DoAction returncode:" + r.returnCode + ", message:" + r.message);
                return isError;
            }

            ResponseData data = null;
            if (Wait(id.Value, timeout.Value, ref data) == true)
            {
                if (WaitStatus(status) == true)
                {
                    isError = false;
                }
            }

            addLog_Response(id.Value, command, data, isAddlog);
            //addLog(id.Value, command, command + " (recieve)", returnEnum + " (" + r.returnCode + "), message:" + r.message, isAddlog);

            return isError;
        }

        /// <summary>
        /// Execute Method
        /// </summary>
        /// <param name="act">deligate of method</param>
        /// <returns>MessageData</returns>
        public ResponseData DoFunc(string command, Func<int, SiLAReturnValue> act, State? status = State.Idle, int? id = null, bool isAddlog = false)
        {
            if (id == null)
            {
                id = GetId();
            }

            TimeSpan timeout = TimeSpan.FromMilliseconds(180000);

            //addLog(id.Value, command, command + " (send)", null, isAddlog);

            var r = act(id.Value);
            var returnEnum = (ReturnCodes)Enum.ToObject(typeof(ReturnCodes), r.returnCode);

            addLog(id.Value, command, command, returnEnum + " (" + r.returnCode + "), message:" + r.message, isAddlog);
            //addLog(id.Value, command, command + " (recieve)", returnEnum + " (" + r.returnCode + "), message:" + r.message, isAddlog);

            if (r.returnCode != (int)ReturnCodes.AsyncAccepted)
            {
                System.Diagnostics.Debug.WriteLine("DoFunc returncode:" + r.returnCode + ", message:" + r.message);
                return null;
            }

            ResponseData data = null;
            if (Wait(id.Value, timeout, ref data) == true)
            {
                if (status.HasValue == true)
                {
                    WaitStatus(status.Value);
                }
            }

            addLog_Response(id.Value, command, data, isAddlog);

            return data;
        }

        bool WaitStatus(State correctStatus)
        {
            var id = GetId();
            TimeSpan timeout = TimeSpan.FromMilliseconds(180000);
            var watch = new System.Diagnostics.Stopwatch();
            watch.Restart();
            while (true)
            {
                string deviceid = string.Empty;
                State state = State.Startup;
                CommandDescription[] substates;
                bool locked = false;
                string pmsID = string.Empty;
                DateTime currentTime = DateTime.MinValue;

                GetStatus(id, out deviceid, out state, out substates, out locked, out pmsID, out currentTime);

                if (state == correctStatus)
                {
                    return true;
                }

                if (watch.Elapsed > timeout)
                {
                    return false;
                }
                System.Threading.Thread.Sleep(sleepTime_ms);
            }
        }

        private object syncobj = new object();

        SiLAReturnValue GetStatus(int requestID, out string deviceId, out State state, out CommandDescription[] subStates, out bool locked, out string PMSId, out DateTime currentTime, bool isAddlog = false)
        {
            lock (syncobj)
            {
                var ret = client.Proxy.GetStatus(requestID, out deviceId, out state, out subStates, out locked, out PMSId, out currentTime);

                var returnEnum = (ReturnCodes)Enum.ToObject(typeof(ReturnCodes), ret.returnCode);
                addLog(requestID, "GetStatus", "GetStatus", returnEnum + " (" + ret.returnCode + "), " + "State:" + state + ", Sub:" + subStates + ", IsLocked:" + locked, isAddlog);

                return ret;
            }
        }

        public State? GetStatus(out bool? deviceLocked)
        {
            string deviceid = string.Empty;
            State state = State.Startup;
            CommandDescription[] substates;
            bool locked = false;
            string pmsID = string.Empty;
            DateTime currentTime = DateTime.MinValue;
            deviceLocked = null;

            GetStatus(GetId(), out deviceid, out state, out substates, out locked, out pmsID, out currentTime);
            deviceLocked = locked;
            return state;
        }

        /// <summary>
        /// Wait completion of method
        /// </summary>
        /// <param name="id">id</param>
        /// <param name="timeout">timeout</param>
        /// <param name="data">MessageData</param>
        /// <returns>be a timeout or nut</returns>
        bool Wait(int id, TimeSpan timeout, ref ResponseData data)
        {
            var watch = new System.Diagnostics.Stopwatch();
            watch.Restart();
            while (true)
            {
                var list = evObj.CompletedIDDic;
                if (list.ContainsKey(id) == true)
                {
                    var val = string.Empty;
                    list.TryRemove(id, out val);
                    data = ConvertToMessageData(val);
                    return true;
                }

                if (watch.Elapsed > timeout)
                {
                    return false;
                }
                System.Threading.Thread.Sleep(sleepTime_ms);
            }
        }
        const int sleepTime_ms = 100;
        /// <summary>
        /// Wait completion of method
        /// </summary>
        /// <param name="id">id</param>
        /// <param name="timeout">timeout</param>
        /// <returns>be a timeout or nut</returns>
        bool Wait(int id, TimeSpan timeout)
        {
            var watch = new System.Diagnostics.Stopwatch();
            watch.Restart();
            while (true)
            {
                var list = evObj.CompletedIDDic;
                if (list.ContainsKey(id) == true)
                {
                    var val = string.Empty;
                    list.TryRemove(id, out val);
                    ConvertToMessageData(val);
                    return true;
                }

                if (watch.Elapsed > timeout)
                {
                    return false;
                }
                System.Threading.Thread.Sleep(sleepTime_ms);
            }
        }

        /// <summary>
        /// Convert to MessageData
        /// </summary>
        /// <param name="val">xml string</param>
        /// <returns>MessageData</returns>
        private ResponseData ConvertToMessageData(string val)
        {
            ResponseData data = null;
            if (String.IsNullOrEmpty(val)) { return data; }

            var o = Util.DeserializeXML(val, typeof(ResponseData));
            if (o is ResponseData)
            {
                data = o as ResponseData;
            }
            return data;
        }

        /// <summary>
        /// Set parameters
        /// </summary>
        /// <param name="requestID">id</param>
        /// <param name="lockID">lock id</param>
        /// <param name="name">method name</param>
        /// <param name="parameters">parameters</param>
        public SiLAReturnValue SetParameter(int requestID, string lockID, string[] names, params object[] parameters)
        {
            var xmll = MakeParameterString(names, parameters);

            return client.Proxy.SetParameters(requestID, lockID, xmll);
        }

        public static string MakeParameterString(string[] name, params object[] parameters)
        {
            var paramList = new List<Parameter>();

            for (int i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                paramList.Add(new Parameter
                {
                    Item = p,
                    ItemElementName = p is string ? ItemChoiceType.String : p is int ? ItemChoiceType.Int32 : p is bool ? ItemChoiceType.Boolean : ItemChoiceType.String,
                    name = i <= name.Length - 1 ? name[i] : null,
                    parameterType = p is string ? AllowedType.String : p is int ? AllowedType.Int32 : p is bool ? AllowedType.Boolean : AllowedType.String,
                    parameterTypeSpecified = true,
                });
            }

            var paramset = new ParameterSet() { Parameter = paramList.ToArray() };

            var xmll = Util.GetXMLSelializedString(typeof(ParameterSet), paramset);
            return xmll;
        }


        private void evObj_OccurredResponse(int arg1, SiLAReturnValue r, string responseData)
        {
            var returnEnum = (ReturnCodes)Enum.ToObject(typeof(ReturnCodes), r.returnCode);
            addLog(arg1, "ResponseEvent", returnEnum + " (" + r.returnCode + ")", r.message, true);
        }

        private void evObj_OccurredError(int arg1, SiLAReturnValue r)
        {
            var returnEnum = (ReturnCodes)Enum.ToObject(typeof(ReturnCodes), r.returnCode);
            addLog(arg1, "ErrorEvent", returnEnum + " (" + r.returnCode + ")", r.message, true);
        }
    }

    /// <summary>
    /// Implementation of IEventService
    /// </summary>
    public class EventReceiverService : IEventService
    {
        public ConcurrentDictionary<int, string> CompletedIDDic = new ConcurrentDictionary<int, string>();

        public event Action<int, SiLAReturnValue, string> OccurredResponseEvent = (id, returnVal, responseData) => { };
        public event Action<int, SiLAReturnValue> OccurredError = (id, returnVal) => { };

        public SiLAReturnValue DataEvent(int requestID, string datavalue)
        {
            throw new NotImplementedException();
        }

        public SiLAReturnValue StatusEvent(string deviceID, SiLAReturnValue returnValue, string eventDescription)
        {
            System.Diagnostics.Debug.WriteLine("#### EventReceiverService StatusEvent debug deviceID= " + deviceID + ", retval=" + returnValue.returnCode + ", message=" + returnValue.message);
            return new SiLAReturnValue() { returnCode = (int)ReturnCodes.Success, };
        }

        public SiLAReturnValue ResponseEvent(int requestID, SiLAReturnValue returnValue, string responseData)
        {
            //System.Threading.Thread.Sleep(10000);
            System.Diagnostics.Debug.WriteLine("#### EventReceiverService ResponseEvent debug requestID= " + requestID + ", retval=" + returnValue.returnCode + ", message=" + returnValue.message);
            CompletedIDDic.TryAdd(requestID, responseData);
            OccurredResponseEvent(requestID, returnValue, responseData);
            return new SiLAReturnValue() { returnCode = (int)ReturnCodes.Success, };
        }

        public SiLAReturnValue ErrorEvent(int requestID, SiLAReturnValue returnValue, out string continuationTask)
        {
            // continuationTask xmldata
            continuationTask = "Repeat"; // debug
            OccurredError(requestID, returnValue);
            System.Diagnostics.Debug.WriteLine("#### EventReceiverService ErrorEvent debug requestID= " + requestID + ", retval=" + returnValue.returnCode + ", message=" + returnValue.message);
            return new SiLAReturnValue() { returnCode = (int)ReturnCodes.Success, };
        }
    }

    public class ServiceClient<T> : System.ServiceModel.ClientBase<T> where T : class
    {

        public ServiceClient()
        {
        }

        public ServiceClient(string endpointConfigurationName) :
            base(endpointConfigurationName)
        {
        }

        public ServiceClient(string endpointConfigurationName, string remoteAddress) :
            base(endpointConfigurationName, remoteAddress)
        {
        }

        public ServiceClient(string endpointConfigurationName, EndpointAddress remoteAddress) :
            base(endpointConfigurationName, remoteAddress)
        {
        }

        public ServiceClient(System.ServiceModel.Channels.Binding binding, EndpointAddress remoteAddress) :
            base(binding, remoteAddress)
        {
        }

        public T Proxy
        {
            get { return this.Channel; }
        }
    }

    public class InspectorBehavior : IEndpointBehavior
    {
        public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
        {
            //throw new NotImplementedException();
        }

        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {
            clientRuntime.MessageInspectors.Add(new MyMessageInspector());
        }

        public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
        {
            //throw new NotImplementedException();
        }

        public void Validate(ServiceEndpoint endpoint)
        {
            //throw new NotImplementedException();
        }
    }

    public class MyMessageInspector : IClientMessageInspector
    {
        public void AfterReceiveReply(ref Message reply, object correlationState)
        {
            //   throw new NotImplementedException();
        }

        public object BeforeSendRequest(ref Message request, IClientChannel channel)
        {
            var res = request.ToString();
            return null;
            //throw new NotImplementedException();
        }

        private bool Count()
        {
            for (int i = 0; i < 1000; i++)
            {
                Thread.Sleep(500);
            }

            return true;
        }

        private void RunTask()
        {
            ////////////////// Fast way. Run Task
            Task.Run(() =>
            {
                Count();
            });

            // Older framework use this syntax
            Task.Factory.StartNew(() =>
            {
                Count();
            });


            ////////////////// Recommended way. To wait, save task as variable. 
            Task<bool> task1 = new Task<bool>(Count);

            task1.Wait();

            var result = task1.Result;

        }

    }




}
