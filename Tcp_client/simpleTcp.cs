// See https://aka.ms/new-console-template for more information

using System;
using System.Net.Sockets;
using System.Net;

/*
 * TCP IP 통신
 *  tcp /udp 통신의 tcp 통신 udp보다 느리지만 신뢰성좋음
 *  socket osi7계층 4번째 정처기때보던 그거 맞음. 
 *  패킷의 기본구조 헤더 + 데이터 (진짜 간략함)
 *  여러가지 유효성 항목들도 존재함 ex.checksum start/end 등
*/

namespace TcpClient
{
    public class SimpleTcp
    {
        public Socket socket = null;
        public string LatestErrorMsg;


        //소켓 연결
        public bool Connect(string ip, int port) //요청할 ip와 port번호
        {
            try
            {
                IPAddress serverIP = IPAddress.Parse(ip);
                int serverPort = port;

                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);// 소켓에 네트워크 유형, 데이터연결 ,특정 프로토콜을 정의 일반적인 ip통신에선  AddressFamily.InterNetwork 사용
                socket.Connect(new IPEndPoint(serverIP, serverPort)); //원격 호스트에 대한 연결을 설정.

                if(socket == null || socket.Connected == false) // 소켓에 설정이 안되거나 null일시
                {
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                LatestErrorMsg = ex.Message; //마지막 에러 메세지 담기
                return false;
            }
        }

        public Tuple<int, byte[]> Receive() //c# Recive()의경우 서버에서 보낸 바인딩 데이터를 받을때 사용 참고자료 https://learn.microsoft.com/ko-kr/dotnet/api/system.net.sockets.socket.receive?view=net-8.0
        {
            try
            {
                byte[] ReadBuffer = new byte[2048];
                var nRecv = socket.Receive(ReadBuffer,0,ReadBuffer.Length,SocketFlags.None); //Receive()호출시 동기IO 기때문에 서버에서 응답을 줄 때 까지 멈춤 그렇기 때문에 다른 워크 스레드를 만들어서 호출 하여야함.

               if(nRecv == 0) //nRecv의 값이 0이면 null반환
                {
                    return null;
                }

                return Tuple.Create(nRecv, ReadBuffer);
            }
            catch (SocketException se)
            {
                LatestErrorMsg = se.Message; //소켓 에러메세지 변수에 담음
            }

            return null;
        }

        //스트림에 쓰기
        public void Send(byte[] sendData)
        {
            try
            {
                if(socket != null && socket.Connected) //연결 상태 유무 확인
                {
                    socket.Send(sendData, 0, sendData.Length, SocketFlags.None); //send의경우에도 동기IO지만 조절가능 
                }
                else
                {
                    LatestErrorMsg = "먼저 채팅서버에 접속필요.";
                }
            }
            catch(SocketException se)
            {
                LatestErrorMsg = se.Message; 
            }

        }
        //소켓 및 스트림 닫기
        public void Close()
        {
            if (socket != null && socket.Connected)
            {
                //Sock.Shutdown(SocketShutdown.Both);
                socket.Close();
            }
        }

        public bool IsConnected() {  //현재 소켓이이 연결중인지 아닌지 bool 반환
            return (socket != null && socket.Connected) ? true : false; 
        }

    }
 








}

