using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Kesco.Lib.Win.Receive
{
    /// <summary>
    ///   Summary description for GetIpToServer.
    /// </summary>
    public class GetIpToServer
    {
        #region win32

        private const int NO_ERROR = 0;
        private const int MIB_TCP_STATE_ESTAB = 5;

        public struct MIB_TCPTABLE
        {
            public int dwNumEntries;
            public MIB_TCPROW[] table;
        }

        public struct MIB_TCPROW
        {
            public string StrgState;
            public int iState;
            public IPEndPoint Local;
            public IPEndPoint Remote;
        }

        [DllImport("iphlpapi.dll", SetLastError = true)]
        public static extern int GetTcpTable(byte[] pTcpTable, out int pdwSize, bool bOrder);

        public static MIB_TCPTABLE GetTable(byte[] buffer)
        {
            var TcpConnetion = new MIB_TCPTABLE();

            int nOffset = 0;
            // number of entry in the
            TcpConnetion.dwNumEntries = Convert.ToInt32(buffer[nOffset]);
            nOffset += 4;
            TcpConnetion.table = new MIB_TCPROW[TcpConnetion.dwNumEntries];

            for (int i = 0; i < TcpConnetion.dwNumEntries; i++)
            {
                int st = Convert.ToInt32(buffer[nOffset]);
                TcpConnetion.table[i].StrgState = st.ToString();
                TcpConnetion.table[i].iState = st;
                nOffset += 4;
                string LocalAdrr = buffer[nOffset].ToString() + "." + buffer[nOffset + 1].ToString() + "." +
                                   buffer[nOffset + 2].ToString() + "." + buffer[nOffset + 3].ToString();
                nOffset += 4;
                int LocalPort = ((buffer[nOffset]) << 8) + ((buffer[nOffset + 1]));
                nOffset += 4;
                TcpConnetion.table[i].Local = new IPEndPoint(IPAddress.Parse(LocalAdrr), LocalPort);

                string RemoteAdrr = buffer[nOffset].ToString() + "." + buffer[nOffset + 1].ToString() + "." +
                                    buffer[nOffset + 2].ToString() + "." + buffer[nOffset + 3].ToString();
                nOffset += 4;
                int RemotePort;
                if (RemoteAdrr == "0.0.0.0")
                {
                    RemotePort = 0;
                }
                else
                {
                    RemotePort = ((buffer[nOffset]) << 8) + ((buffer[nOffset + 1]));
                }
                nOffset += 4;
                TcpConnetion.table[i].Remote = new IPEndPoint(IPAddress.Parse(RemoteAdrr),
                                                                         RemotePort);
            }
            return TcpConnetion;
        }

        #endregion

		public static bool TestConnection(string connectionString, out string ipAddress)
		{
			ipAddress = null;

			using(var c = new SqlConnection(connectionString))
			{
				try
				{
					c.Open();
					if(c.State == ConnectionState.Open)
					{
						var server = c.DataSource;
						var nis = NetworkInterface.GetAllNetworkInterfaces();
						var check = false;
						foreach(var ni in nis)
						{
							if(ni.OperationalStatus != OperationalStatus.Up)
								continue;
							if(ni.GetIPProperties().GatewayAddresses.Count > 0 &&
								ni.GetIPProperties().GatewayAddresses.Count(x => x.Address.AddressFamily == AddressFamily.InterNetwork) > 0 &&
								ni.GetIPProperties().UnicastAddresses.Count > 0 &&
								ni.GetIPProperties().UnicastAddresses.Count(x => x.Address.AddressFamily ==  AddressFamily.InterNetwork && !IPAddress.IsLoopback(x.Address) &&
								(x.IPv4Mask.Address & x.Address.Address) == (ni.GetIPProperties().GatewayAddresses.Where(z => z.Address.AddressFamily == AddressFamily.InterNetwork).First().Address.Address & x.IPv4Mask.Address)) == 1)
								if(ipAddress == null)
									ipAddress = ni.GetIPProperties().UnicastAddresses.Where(x => x.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(x.Address)
										&& (x.IPv4Mask.Address & x.Address.Address) == (ni.GetIPProperties().GatewayAddresses.Where(z => z.Address.AddressFamily == AddressFamily.InterNetwork).First().Address.Address & x.IPv4Mask.Address)).First().Address.ToString();
								else
								{
									check = true;
									break;
								}
						}
						if(!check) return !string.IsNullOrEmpty(ipAddress);

						int pdwSize;
						Console.WriteLine("{0}: GetTcpTable size start", DateTime.Now.ToString("HH:mm:ss fff"));
						int res = GetTcpTable(null, out pdwSize, true);
						Console.WriteLine("{0}: GetTcpTable size end. Size {1}", DateTime.Now.ToString("HH:mm:ss fff"), pdwSize);
						if(res != 0)
						{
							pdwSize = (int)(pdwSize * 1.3);
							var buffer = new byte[pdwSize];
							res = GetTcpTable(buffer, out pdwSize, true);
							if(res == 0)
							{
								Console.WriteLine("{0}: GetTcpTable start", DateTime.Now.ToString("HH:mm:ss fff"));
								MIB_TCPTABLE tab = GetTable(buffer);
								IPHostEntry iph = Dns.GetHostEntry(server);
								for(int i = 0; i < tab.dwNumEntries; i++)
								{
									if(tab.table[i].iState == MIB_TCP_STATE_ESTAB &&
										tab.table[i].Local.Address.AddressFamily == AddressFamily.InterNetwork &&
										tab.table[i].Remote.Port == 1433)
									{
										//Console.WriteLine("{0}: GetHostEntry start. Ip = {1}", DateTime.Now.ToString("HH:mm:ss fff"), tab.table[i].Remote.Address);
										//IPHostEntry iph = Dns.GetHostEntry(tab.table[i].Remote.Address);
										//Console.WriteLine("{0}: GetHostEntry end.", DateTime.Now.ToString("HH:mm:ss fff"));
										//Match m = Regex.Match(iph.HostName, "^" + server + "(.[0-9a-z]|$)", RegexOptions.IgnoreCase);
										//if (m.Success)
										//{
										if(iph.AddressList.Contains(tab.table[i].Remote.Address))
										{
											ipAddress = tab.table[i].Local.Address.ToString();
											break;
										}
									}
								}
								Console.WriteLine("{0}: GetTcpTable end.", DateTime.Now.ToString("HH:mm:ss fff"));
							}
						}
						return !string.IsNullOrEmpty(ipAddress);
					}
				}
				catch
				{
				}
				finally
				{
					c.Close();
				}
			}
			return false;
		}
    }
}