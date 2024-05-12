using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace TcpClient
{
    //recive에서 받은 데이터를 저장
    class PacketBufferManager
    {
        int BufferSize = 0;
        int ReadPos = 0;
        int WritePos = 0;

        int HeaderSize = 0;
        int MaxPacketSize = 0;
        byte[] PacketData;
        byte[] PacketDataTemp;


        //버퍼 초기화
        public bool Init(int size, int headerSize, int maxPacketSize)//1.패킷버퍼의사이즈 2.헤더사이즈 3.최대패킷크기
        {
            if(size < (maxPacketSize * 2) || size <1 || headerSize <1 || maxPacketSize <1)
            {
                return false;
            }

            BufferSize = size;
            PacketData = new byte[size];
            PacketDataTemp = new byte[size];
            HeaderSize = headerSize;
            MaxPacketSize = maxPacketSize;

            return true;
        }


        //버퍼 쓰기
        public bool Write(byte[] data, int pos, int size)
        {
            if(data == null || (data.Length < (pos + size)))
            {
                return false;
            }

            var remainBufferSize = BufferSize - WritePos;

            if (remainBufferSize < size)
            {
                return false;
            }


            Buffer.BlockCopy(data, pos, PacketData, WritePos, size);
            WritePos += size;


            //남은 패킷의 크기가 남은 공간보다 크면 버퍼를 다시 준비
            if(NextFree() == false)
            {
                BufferRelocate();
            }

            return true;
        }

        public ArraySegment<byte> Read()
        {
            var enableReadSize = WritePos - ReadPos;

            if  (enableReadSize < HeaderSize)
            {
                return new ArraySegment<byte>();
            }

            var packetDataSize = BitConverter.ToInt16(PacketData, ReadPos);
            if (enableReadSize < packetDataSize)
            {
                return new ArraySegment<byte>();
            }

            var completePacketData = new ArraySegment<byte>(PacketData, ReadPos, packetDataSize);
            ReadPos += packetDataSize;
            return completePacketData;
        }
        bool NextFree()
        {
            var enableWriteSize = BufferSize - WritePos;

            if (enableWriteSize < MaxPacketSize)
            {
                return false;
            }

            return true;
        }

        void BufferRelocate()
        {
            var enableReadSize = WritePos - ReadPos;

            //임시버퍼를생성하여  버퍼의 맨 앞에 현재까지 받은 패킷을 저장  
            Buffer.BlockCopy(PacketData, ReadPos, PacketDataTemp, 0, enableReadSize);
            Buffer.BlockCopy(PacketDataTemp, 0, PacketData, 0, enableReadSize);

            //ReadPos 버퍼에있는 패킷의 어느부분부터 읽어올지, writepos => write함수에서 어느부분을 서버에서보낸 패킷의 어느부분을 복사할지
            ReadPos = 0;
            WritePos = enableReadSize;
        }


    }
}
