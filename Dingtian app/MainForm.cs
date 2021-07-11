using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Xml;

namespace Dingtian
{
    public partial class MainForm : Form
    {
        DingTian ding;


        const string configName = "configuration.xml";

        public MainForm()
        {
            InitializeComponent();
            notify.Visible = false;
            notify.Icon = Properties.Resources.Microsoft_Antispyware;
            notify.Text = Text;
        }

        private void buttonRelay_Click(object sender, EventArgs e)
        {
            ((DingRelay)((Button)sender).Tag).ToggleState();
        }

        private void trayRelay_Click(object sender, EventArgs e)
        {
            ((DingRelay)((ToolStripMenuItem)sender).Tag).ToggleState();
        }

        private void trayExit_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            if (!Init())
                Close();
        }



        bool Init()
        {
            if (File.Exists(configName))
            {
                //load configuration
                XmlDocument xml = new XmlDocument();
                xml.Load(configName);

                XmlNode hostNode = xml.DocumentElement.SelectSingleNode("controller/host");
                XmlNode passwNode = xml.DocumentElement.SelectSingleNode("controller/password");

                if (hostNode != null)
                {
                    ding = new DingTian();

                    ding.Host = hostNode.InnerText;

                    ushort pwd = 0;
                    if (ushort.TryParse(passwNode.InnerText, out pwd))
                    {
                        ding.Passw = pwd;
                    }

                    XmlNode dataNode = xml.DocumentElement.SelectSingleNode("controller/show_txrx_data");
                    if (dataNode != null)
                    {
                        bool flag = false;
                        if (bool.TryParse(dataNode.InnerText, out flag))
                            ding.SHOW_DATA = flag;
                    }

                    int BROW = 0;
                    int BCOLUMN = 0;
                    int BCOUNT = 1;

                    foreach (XmlNode relayNode in xml.DocumentElement.SelectNodes("relays/relay"))
                    {
                        byte RelayPort = 0;
                        if (byte.TryParse(relayNode.Attributes["port"].Value, out RelayPort))
                        {

                            if (BCOUNT <= 8)
                            {
                                tableLay.RowStyles[0].SizeType = SizeType.Percent;
                                tableLay.RowStyles[0].Height = 50;
                                tableLay.ColumnStyles[0].SizeType = SizeType.Percent;
                                tableLay.ColumnStyles[0].Width = 50;
                                int newButtonColumn = BCOLUMN;
                                int newButtonRow = BROW;
                                if (tableLay.RowCount < 4)
                                {
                                    if (tableLay.Controls.Count != 0)
                                    {
                                        tableLay.RowCount = tableLay.RowCount + 1;
                                        tableLay.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
                                        newButtonRow = ++BROW;
                                    }
                                }
                                else
                                {
                                    if (tableLay.ColumnCount == 1)
                                    {
                                        BROW = 0;
                                        newButtonRow = BROW;
                                        tableLay.ColumnCount = tableLay.ColumnCount + 1;
                                        tableLay.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
                                        newButtonColumn = ++BCOLUMN;
                                    }
                                    else
                                        newButtonRow = ++BROW;
                                }


                                Button nb = new Button();
                                tableLay.Controls.Add(nb, newButtonColumn, newButtonRow);
                                nb.ImageList = imageList1;
                                nb.ImageIndex = 3;
                                nb.Dock = DockStyle.Fill;

                                nb.Click += buttonRelay_Click;
                                nb.Text = relayNode.Attributes["name"].Value;
                                nb.TextImageRelation = TextImageRelation.ImageAboveText;
                                nb.Font = new Font(nb.Font, FontStyle.Bold);
                                nb.ImageAlign = ContentAlignment.MiddleCenter;
                                nb.TextAlign = ContentAlignment.MiddleCenter;

                                if (notify.ContextMenuStrip == null)
                                    notify.ContextMenuStrip = new ContextMenuStrip();

                                int i = notify.ContextMenuStrip.Items.Add(new ToolStripMenuItem(nb.Text, nb.Image, trayRelay_Click));


                                nb.Tag = ding.ReristerRelay(nb.Text, RelayPort, nb, (ToolStripMenuItem)notify.ContextMenuStrip.Items[i]);
                                notify.ContextMenuStrip.Items[i].Tag = nb.Tag;

                                BCOUNT++;
                            }
                        }
                    }
                    if (ding.RegisteredRelaysCount != 0)
                    {
                        int k = ding.RegisteredRelaysCount <= 4 ? ding.RegisteredRelaysCount - 1 : 3;

                        MinimumSize = new Size(MinimumSize.Width, MinimumSize.Height + 100 * k);

                        if (tableLay.ColumnCount != 1)
                            MinimumSize = new Size(MinimumSize.Width + 150, MinimumSize.Height);

                        Size = MinimumSize;

                        statusMessage.Text = "Ready";


                        ding.OnReceiveStatus += Ding_OnReceiveStatus;
                        ding.OnSendStatus += Ding_OnSendStatus;

                        BuildTrayMenu();

                        ding.Start();

                        buttonUpdateStatus_Click(null, null);

                        return true;
                    }
                    else
                    {
                        MessageBox.Show(this, "No relays defined", "STARTUP", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }
                }
                else
                {
                    MessageBox.Show(this, "Host not defined!", "STARTUP", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
            }
            else
            {
                MessageBox.Show(this, "Configuration file not found!", "STARTUP", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        void BuildTrayMenu()
        {
            notify.MouseClick += Notify_MouseClick;
            notify.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            notify.ContextMenuStrip.Items.Add(new ToolStripMenuItem("Exit", null, trayExit_Click));
        }

        //restore app on tray icon click
        private void Notify_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Show();
                if (WindowState == FormWindowState.Minimized)
                    WindowState = FormWindowState.Normal;
                BringToFront();
            }
        }


        private void Ding_OnSendStatus(object sender, string status)
        {
            statusMessage.Text = status;
        }

        private void Ding_OnReceiveStatus(object sender, string status)
        {
            statusMessage.Text = status;
        }

        private void buttonUpdateStatus_Click(object sender, EventArgs e)
        {
            ding.Send(DingtianCommand.ReadRelayStatus);
        }

        private void buttonAllOff_Click(object sender, EventArgs e)
        {
            ding.Send(DingtianCommand.WriteRelayStatus);
        }

        private void buttonAllOn_Click(object sender, EventArgs e)
        {
            ding.Send(DingtianCommand.WriteRelayStatus, DefaultState: true);
        }

        private void MainForm_LocationChanged(object sender, EventArgs e)
        {
            notify.Visible = ((Form)sender).WindowState == FormWindowState.Minimized;

            if (((Form)sender).WindowState == FormWindowState.Minimized)
                Hide();
        }


    }


}
