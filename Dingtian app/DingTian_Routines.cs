using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Dingtian
{
    //handle device relay
    public class DingRelay
    {
        //UI button ref
        Button indicator;
        //UI tray menu ref
        ToolStripMenuItem tray;

        //relay state
        bool state;
        //relay name (for fusage)
        string name;
        //relay index in device (starts from 1)
        byte index;


        DingTian parent;



        public string Name { set { name = value; } }
        public byte Index { get { return (byte)(index - 1); } }

        public bool State { get { return state; } }

        //constructor
        public DingRelay(byte _index, Button _indicButton, ToolStripMenuItem _tray, DingTian _parent)
        {
            indicator = _indicButton;
            tray = _tray;
            index = _index;
            parent = _parent;
        }

        //switch relay state
        public void ToggleState()
        {
            state = !state;
            parent.Send(DingtianCommand.WriteRelayStatus, this);
        }
        public void SetState(bool _state)
        {
            state = _state;
            UpdateIndicator();
        }

        //update UI controls
        void UpdateIndicator()
        {
            indicator.ImageIndex = state ? 0 : 1;
            if (tray != null)
                tray.Image = indicator.Image;
        }
    }

    //TX RX status delegates
    public delegate void DingTianSendStatusDelegate(object sender, string status);
    public delegate void DingTianReceiveStatusDelegate(object sender, string status);

    public class DingTian
    {
        string host;
        ushort passw;
        UdpClient userver;

        List<DingRelay> RELAYS = new List<DingRelay>();

        public bool SHOW_DATA = false;

        public event DingTianSendStatusDelegate OnSendStatus;
        public event DingTianReceiveStatusDelegate OnReceiveStatus;



        public byte RegisteredRelaysCount { get { return (byte)RELAYS.Count; } }

        public DingRelay ReristerRelay(string RelayName, byte RelayIndex, Button UIbutton, ToolStripMenuItem UItray)
        {
            RELAYS.Add(new DingRelay(RelayIndex, UIbutton, UItray, this));
            RELAYS.Last().Name = RelayName;

            return RELAYS.Last();
        }

        public DingTian()
        {
            userver = new UdpClient(60001, AddressFamily.InterNetwork);
        }

        public string Host { set { host = value; } }
        public ushort Passw { set { passw = value; } }

        //start udp reseive
        public void Start()
        {
            if (!string.IsNullOrEmpty(host) && passw != 0)
                Receive();
        }

        private async Task Receive()
        {
            while (true)
            {
                UdpReceiveResult result = await userver.ReceiveAsync();

                if (OnReceiveStatus != null)
                    OnReceiveStatus(this, string.Format("RX {0} {1}", result.RemoteEndPoint.Address.ToString(),
                        SHOW_DATA ? BitConverter.ToString(result.Buffer).Replace("-", string.Empty) : ""));

                ProcessReceivedData(result.Buffer);

            }
        }

        //process udp data from device
        void ProcessReceivedData(byte[] data)
        {
            using (MemoryStream ms = new MemoryStream(data))
            {
                using (BinaryReader re = new BinaryReader(ms))
                {
                    byte r_command = re.ReadByte();
                    byte r_result = re.ReadByte();
                    byte r_session = re.ReadByte();
                    byte resp_command = re.ReadByte();

                    DingtianCommand comm = (DingtianCommand)resp_command;
                    switch (comm)
                    {
                        case DingtianCommand.ReadRelayStatus:
                            {
                                byte r_status = re.ReadByte();

                                BitArray relays = new BitArray(new byte[] { r_status });

                                //set actual states of relays
                                RELAYS.ForEach(rel => rel.SetState(relays.Get(rel.Index)));


                                break;

                            }
                        case DingtianCommand.WriteRelayStatus:
                            {
                                //get actual relay status after operation
                                Send(DingtianCommand.ReadRelayStatus);

                                break;
                            }
                    }

                }
            }
        }

        //send data to device
        public async Task Send(DingtianCommand command, DingRelay relay = null, bool DefaultState = false)
        {
            if (!string.IsNullOrEmpty(host) && passw != 0)
            {
                byte i_command = 0xFF;
                byte i_result = 0xAA;
                byte i_session = 0;
                byte re_Command = (byte)command;

                using (MemoryStream buffer = new MemoryStream())
                {
                    using (BinaryWriter bw = new BinaryWriter(buffer))
                    {
                        bw.Write(i_command);
                        bw.Write(i_result);

                        bw.Write(i_session);
                        bw.Write(re_Command);
                        bw.Write(passw);

                        if (command == DingtianCommand.WriteRelayStatus)
                        {
                            BitArray mask = new BitArray(new bool[8]);
                            if (relay == null)
                                RELAYS.ForEach(rel => mask.Set(rel.Index, true));

                            else
                                mask.Set(relay.Index, true);

                            byte[] b_mask = new byte[1];
                            mask.CopyTo(b_mask, 0);

                            bw.Write(b_mask[0]);

                            BitArray values = new BitArray(new bool[8]);

                            if (relay != null)
                                values.Set(relay.Index, relay.State);
                            else
                                RELAYS.ForEach(rel => values.Set(rel.Index, DefaultState));

                            byte[] b_value = new byte[1];
                            values.CopyTo(b_value, 0);

                            bw.Write(b_value[0]);
                        }

                        buffer.Flush();

                        byte[] data = buffer.ToArray();

                        if (OnSendStatus != null)
                            OnSendStatus(this, string.Format("TX {0} {1}", host,
                                 SHOW_DATA ? BitConverter.ToString(data).Replace("-", string.Empty) : ""));

                        int res = await userver.SendAsync(data, (int)data.Length, host, 60001);
                    }

                }
            }
        }
    }

    //relay commands
    public enum DingtianCommand { ReadRelayStatus = 0, WriteRelayStatus = 1 }

}
