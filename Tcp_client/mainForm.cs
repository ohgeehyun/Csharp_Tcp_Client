using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace TcpClient
{
    public partial class mainForm : Form
    {
        SimpleTcp Network = new SimpleTcp();

        bool IsNetworkThreadRunning = false;
        bool IsBackGroundProcessRunning = false;

        System.Threading.Thread NetworkReadThread = null;//recive()함수 계속 호출.데이터가 있으면 PacketBufferManager에 복사
        System.Threading.Thread NetworkSendThread = null;//굳이 send는 보내필요없지만 서버에 문제가생겨 버퍼가 꽉차서 데이터를 못보내거나 하는 경우 제외 굳이 필요없지만 생성

        PacketBufferManager PacketBuffer = new PacketBufferManager();
        Queue<PacketData> RecvPacketQueue = new Queue<PacketData>();//PacketBufferManager에서 버퍼에 버퍼에 패킷크기보다 이상의 데이터가 있을 경우 큐에 저장
        Queue<byte[]> SendPacketQueue = new Queue<byte[]>();

        System.Windows.Threading.DispatcherTimer dispatcherUITimer;//work스레드에서 ui 접근불가 C++은 가능 C#은 기본적 불가능 TIME을 통하여 패킷이있으면 ui수정


        public mainForm()
        {
            InitializeComponent();
        }

        private void mainForm_Load(object sender, EventArgs e)
        {
            //form실행시 진입점
            PacketBuffer.Init((8096 * 10), PacketDef.PACKET_HEADER_SIZE, 1024); //하나의 패킷의 최대 크기

            IsNetworkThreadRunning = true;
            NetworkReadThread = new System.Threading.Thread(this.NetworkReadProcess); //read용 스레드 생성
            NetworkReadThread.Start();
            NetworkSendThread = new System.Threading.Thread(this.NetworkSendProcess);//send용 스레드 생성 굳이 필요는없지만 통일
            NetworkSendThread.Start();

            IsBackGroundProcessRunning = true;
            //ui수정 관련 처리
            dispatcherUITimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherUITimer.Tick += new EventHandler(BackGroundProcess);
            dispatcherUITimer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            dispatcherUITimer.Start();

            btnDisconnect.Enabled = false;

            SetPacketHandler();
            DevLog.Write("프로그램 시작 !!!", LOG_LEVEL.INFO);
        }

        private void mainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            IsNetworkThreadRunning = false;
            IsBackGroundProcessRunning = false;

            Network.Close();
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            string address = textBoxIP.Text;

            if (checkBoxLocalHostIP.Checked)
            {
                address = "127.0.0.1";
            }

            int port = Convert.ToInt32(textBoxPort.Text);

            if (Network.Connect(address, port))
            {
                labelStatus.Text = string.Format("{0}. 서버에 접속 중", DateTime.Now);
                btnConnect.Enabled = false;
                btnDisconnect.Enabled = true;

                DevLog.Write($"서버에 접속 중", LOG_LEVEL.INFO);
            }
            else
            {
                labelStatus.Text = string.Format("{0}. 서버에 접속 실패", DateTime.Now);
            }
        }

        private void btnDisconnect_Click(object sender, EventArgs e)
        {
            SetDisconnectd();
            Network.Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(textSendText.Text))
            {
                MessageBox.Show("보낼 텍스트를 입력하세요");
                return;
            }

            List<byte> dataSource = new List<byte>();
            dataSource.AddRange(Encoding.UTF8.GetBytes(textSendText.Text));

            SendPacketQueue.Enqueue(dataSource.ToArray());
        }



        void NetworkReadProcess()
        {
            const Int16 PacketHeaderSize = PacketDef.PACKET_HEADER_SIZE;

            while (IsNetworkThreadRunning)
            {
                if (Network.IsConnected() == false)
                {
                    System.Threading.Thread.Sleep(1);
                    continue;
                }

                var recvData = Network.Receive(); //서버에서 응답을 줄 떄 까지 블락

                if (recvData != null)
                {
                    PacketBuffer.Write(recvData.Item2, 0, recvData.Item1);

                    while (true) //실제 서버에서 TCP환경은 stream이기때문에 패킷을 여러개 동시에 보냈을 가능성도 있기때문에 사용가능한 패킷이 되도록 바꿔주는 작업
                    {
                        var data = PacketBuffer.Read();
                        if (data.Count < 1)
                        {
                            break;
                        }

                        var packet = new PacketData();
                        packet.DataSize = (short)(data.Count - PacketHeaderSize);
                        packet.PacketID = BitConverter.ToInt16(data.Array, data.Offset + 2);
                        packet.Type = (SByte)data.Array[(data.Offset + 4)];
                        packet.BodyData = new byte[packet.DataSize];
                        Buffer.BlockCopy(data.Array, (data.Offset + PacketHeaderSize), packet.BodyData, 0, (data.Count - PacketHeaderSize));
                        lock (((System.Collections.ICollection)RecvPacketQueue).SyncRoot)//lock문으로 다른 스레드는 간섭할수없음
                        {
                            RecvPacketQueue.Enqueue(packet);
                        }
                    }
                    //DevLog.Write($"받은 데이터: {recvData.Item2}", LOG_LEVEL.INFO);
                }
                else
                {
                    Network.Close();
                    SetDisconnectd();
                    DevLog.Write("서버와 접속 종료 !!!", LOG_LEVEL.INFO);
                }
            }
        }

        void NetworkSendProcess()
        {
            while (IsNetworkThreadRunning)
            {
                System.Threading.Thread.Sleep(1);

                if (Network.IsConnected() == false)
                {
                    continue;
                }

                lock (((System.Collections.ICollection)SendPacketQueue).SyncRoot)
                {
                    if (SendPacketQueue.Count > 0)
                    {
                        var packet = SendPacketQueue.Dequeue();
                        Network.Send(packet);
                    }
                }
            }
        }


        void BackGroundProcess(object sender, EventArgs e)
        {
            ProcessLog();

            try
            {
                var packet = new PacketData();

                lock (((System.Collections.ICollection)RecvPacketQueue).SyncRoot)
                {
                    if (RecvPacketQueue.Count() > 0)
                    {
                        packet = RecvPacketQueue.Dequeue();
                    }
                }

                if (packet.PacketID != 0)
                {
                    PacketProcess(packet);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format("ReadPacketQueueProcess. error:{0}", ex.Message));
            }
        }

        private void ProcessLog()
        {
            // 너무 이 작업만 할 수 없으므로 일정 작업 이상을 하면 일단 패스한다.
            int logWorkCount = 0;

            while (IsBackGroundProcessRunning)
            {
                System.Threading.Thread.Sleep(1);

                string msg;

                if (DevLog.GetLog(out msg))
                {
                    ++logWorkCount;

                    if (listBoxLog.Items.Count > 512)
                    {
                        listBoxLog.Items.Clear();
                    }

                    listBoxLog.Items.Add(msg);
                    listBoxLog.SelectedIndex = listBoxLog.Items.Count - 1;
                }
                else
                {
                    break;
                }

                if (logWorkCount > 8)
                {
                    break;
                }
            }
        }


        public void SetDisconnectd()
        {
            if (btnConnect.Enabled == false)
            {
                btnConnect.Enabled = true;
                btnDisconnect.Enabled = false;
            }

            SendPacketQueue.Clear();

            listBoxRoomChatMsg.Items.Clear();
            listBoxRoomUserList.Items.Clear();

            labelStatus.Text = "서버 접속이 끊어짐";
        }

        public void PostSendPacket(PACKET_ID packetID, byte[] bodyData)
        {
            if (Network.IsConnected() == false)
            {
                DevLog.Write("서버 연결이 되어 있지 않습니다", LOG_LEVEL.ERROR);
                return;
            }

            Int16 bodyDataSize = 0;
            if (bodyData != null)
            {
                bodyDataSize = (Int16)bodyData.Length;
            }
            var packetSize = bodyDataSize + PacketDef.PACKET_HEADER_SIZE;

            List<byte> dataSource = new List<byte>();
            dataSource.AddRange(BitConverter.GetBytes((Int16)packetSize));
            dataSource.AddRange(BitConverter.GetBytes((Int16)packetID));
            dataSource.AddRange(new byte[] { (byte)0 });

            if (bodyData != null)
            {
                dataSource.AddRange(bodyData);
            }

            SendPacketQueue.Enqueue(dataSource.ToArray());
        }

        void AddRoomUserList(Int64 userUniqueId, string userID)
        {
            var msg = $"{userUniqueId}: {userID}";
            listBoxRoomUserList.Items.Add(msg);
        }

        void RemoveRoomUserList(Int64 userUniqueId)
        {
            object removeItem = null;

            foreach (var user in listBoxRoomUserList.Items)
            {
                var items = user.ToString().Split(':');
                if (Convert.ToInt64(items[0]) == userUniqueId)
                {
                    removeItem = user;
                    return;
                }
            }

            if (removeItem != null)
            {
                listBoxRoomUserList.Items.Remove(removeItem);
            }
        }


        // 로그인 요청
        private void button2_Click(object sender, EventArgs e)
        {
            var loginReq = new LoginReqPacket();
            loginReq.SetValue(textBoxUserID.Text, textBoxUserPW.Text);

            PostSendPacket(PACKET_ID.LOGIN_REQ, loginReq.ToBytes());
            DevLog.Write($"로그인 요청:  {textBoxUserID.Text}, {textBoxUserPW.Text}");
        }

        private void btn_RoomEnter_Click(object sender, EventArgs e)
        {
            var requestPkt = new RoomEnterReqPacket();
            requestPkt.SetValue(Convert.ToInt32(textBoxRoomNumber.Text));

            PostSendPacket(PACKET_ID.ROOM_ENTER_REQ, requestPkt.ToBytes());
            DevLog.Write($"방 입장 요청:  {textBoxRoomNumber.Text} 번");
        }

        private void btn_RoomLeave_Click(object sender, EventArgs e)
        {
            PostSendPacket(PACKET_ID.ROOM_LEAVE_REQ, null);
            DevLog.Write($"방 입장 요청:  {textBoxRoomNumber.Text} 번");
        }

        private void btnRoomChat_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(textBoxRoomSendMsg.Text))
            {
                MessageBox.Show("채팅 메시지를 입력하세요");
                return;
            }

            var requestPkt = new RoomChatReqPacket();
            requestPkt.SetValue(textBoxRoomSendMsg.Text);

            PostSendPacket(PACKET_ID.ROOM_CHAT_REQ, requestPkt.ToBytes());
            DevLog.Write($"방 채팅 요청");
        }

        private void btnRoomRelay_Click(object sender, EventArgs e)
        {
            //if( textBoxRelay.Text.IsEmpty())
            //{
            //    MessageBox.Show("릴레이 할 데이터가 없습니다");
            //    return;
            //}

            //var bodyData = Encoding.UTF8.GetBytes(textBoxRelay.Text);
            //PostSendPacket(PACKET_ID.PACKET_ID_ROOM_RELAY_REQ, bodyData);
            //DevLog.Write($"방 릴레이 요청");
        }

        // 로비 리스트 요청
        private void button3_Click(object sender, EventArgs e)
        {
            PostSendPacket(PACKET_ID.LOBBY_LIST_REQ, null);
            DevLog.Write($"방 릴레이 요청");
        }

        // 로비 입장 요청
        private void button4_Click(object sender, EventArgs e)
        {

        }

        // 로비 나가기 요청
        private void button5_Click(object sender, EventArgs e)
        {

        }

        private void groupBox5_Enter(object sender, EventArgs e)
        {

        }

        private void InitializeComponent_mainform()
        {
            this.SuspendLayout();
            // 
            // mainForm
            // 
            this.ClientSize = new System.Drawing.Size(284, 261);
            this.Name = "mainForm";
            this.Load += new System.EventHandler(this.mainForm_Load_1);
            this.ResumeLayout(false);

        }

        private void mainForm_Load_1(object sender, EventArgs e)
        {

        }

        private void groupBox1_Enter(object sender, EventArgs e)
        {

        }
    }
}
