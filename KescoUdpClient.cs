using System.Net;
using System.Net.Sockets;

namespace Kesco.Lib.Win.Receive
{
    /// <summary>
    /// UDP-клиент для получения сообщений
    /// </summary>
    public class KescoUdpClient : UdpClient
    {
        public KescoUdpClient()
        {
        }

        public KescoUdpClient(IPEndPoint endpoint)
            : base(endpoint)
        {
        }

        public void MyClose()
        {
            try
            {
                Socket ss = Client;
                ss.Shutdown(SocketShutdown.Both);
                ss.Blocking = false;
                Close();
            }
            catch
            {
            }
        }
    }
}