using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UDPPerfTest_Share {
    public class UdpParam {
        private UdpClient udpClient;
        public int LocalPort { get; set; }
        public int HostPort { get; set; }
        public string HostIP { get; set; }
        public int RecvIdleSleepMs { get; set; }

        public int RecvBufSize {
            get => udpClient.Client.ReceiveBufferSize;
            set => udpClient.Client.ReceiveBufferSize = value;
        }

        public int SendBufSize {
            get => udpClient.Client.SendBufferSize;
            set => udpClient.Client.SendBufferSize = value;
        }

        public UdpParam(UdpClient udpClient) {
            this.udpClient = udpClient;
        }
    }

    public class Udp {
        UdpClient udpClient = new UdpClient();
        UdpParam udpParam;

        public event Action<object, byte[]> Recv;
        public event Action<object, Exception> RecvException;
        
        bool recvRun = false;
        Thread recvThread;
        Thread procThread;
        ConcurrentQueue<byte[]> recvBufQ = new ConcurrentQueue<byte[]>();
        
        public Udp(int localPort, int hostPort, string hostIP, int recvIdleSleepMs, int recvBufSize, int sendBufSize) {
            udpParam = new UdpParam(this.udpClient);
            udpParam.LocalPort = localPort;
            udpParam.HostPort = hostPort;
            udpParam.HostIP = hostIP;
            udpParam.RecvIdleSleepMs = recvIdleSleepMs;
            udpParam.RecvBufSize = recvBufSize;
            udpParam.SendBufSize = sendBufSize;
        }

        private void RecvThreadFunc() {
            while (recvRun) {
                try {
                    IPEndPoint localEP = new IPEndPoint(IPAddress.Any, udpParam.LocalPort);
                    var buf = udpClient.Receive(ref localEP);
                    recvBufQ.Enqueue(buf);
                }
                catch (Exception ex) {
                    RecvException?.Invoke(this, ex);
                }
            }
        }

        private void ProcThreadFunc() {
            while (recvRun) {
                if (recvBufQ.TryDequeue(out byte[] buf)) {
                    Recv?.Invoke(this, buf);
                } else {
                    Thread.Sleep(udpParam.RecvIdleSleepMs);
                }
            }
        }

        public void StopRecv() {
            if (!recvRun)
                return;

            recvRun = false;
            recvThread.Join();
            procThread.Join();
        }

        public void StartRecv() {
            if (recvRun)
                return;

            recvRun = true;
            recvThread = new Thread(RecvThreadFunc);
            recvThread.Start();
            procThread = new Thread(ProcThreadFunc);
            procThread.Start();
        }

        public void Send(byte[] buf) {
            IPEndPoint hostEP = new IPEndPoint(IPAddress.Parse(udpParam.HostIP), udpParam.HostPort);
            udpClient.Send(buf, buf.Length, hostEP);
        }
    }
}
