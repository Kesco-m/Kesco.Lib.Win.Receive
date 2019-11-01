using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Kesco.Lib.Win.Receive
{
	/// <summary>
	///   Класс для получения сообщений от БД.
	/// </summary>
	public class Receive : IDisposable
	{
		public delegate void WaitReceiveDelegate(bool start);

		public delegate void ReceivedEvent(string receivedString, int parameter);

		private string connectionString;

        // КодРегистрацииРассылки таблицы РегистрацияРассылки
		private int strID;
		private KescoUdpClient client;
		private Byte[] getByte;
		private string dataTable;
		private int code;
		private int parameter = -1;
		private int parameter2 = -1;
		private int trust;
		private IPEndPoint extPoint;
		private IAsyncResult res;

		public bool Trying { get; private set; }
		public bool Started { get; private set; }

        /// <summary>
        /// Сообщение получено
        /// </summary>
		public event ReceivedEvent Received;

		/// <summary>
		/// Конструктор пустой. Оставлен для историзма
		/// </summary>
		/// <deprecated/>
		/// <param name="connectionString">Строка соединения к бд</param>
		/// <param name="dataTable">Название таблицы регистрации рассылки</param>
		/// <param name="uslovie">Код типа сообщения</param>
		/// <param name="trust">контроль</param>
		 [Obsolete("Receive(string, string, int, int) is deprecated, please use Receive(string, string, int, int, int) instead.", true)]
		 public Receive(string connectionString, string dataTable, int uslovie, int trust) :
			this(connectionString, dataTable, uslovie, -1, trust)
		{
		}

		 /// <summary>
		 /// Конструктор для простой подписки. 
		 /// </summary>
		 /// <param name="connectionString">Строка соединения к бд</param>
		 /// <param name="dataTable">Название таблицы регистрации рассылки</param>
		 /// <param name="uslovie">Код типа сообщения</param>
		 /// <param name="parameter">Параметр для фильтрации получателей сообщения.</param>
		 /// <param name="trust">контроль получения сообщения</param>
		public Receive(string connectionString, string dataTable, int uslovie, int parameter, int trust) :
			this(connectionString, dataTable, uslovie, parameter, -1, trust)
		{

		}

        /// <summary>
        /// Конструктор для подписки с двумя параметрами
        /// </summary>
        /// <param name="connectionString">Строка соединения к бд</param>
        /// <param name="dataTable">Название таблицы регистрации рассылки</param>
        /// <param name="uslovie">Код типа сообщения</param>
        /// <param name="parameter">Параметр для фильтрации получателей сообщения.</param>
        /// <param name="parameter2">Дополнительный араметр для фильтрации получателей сообщения.</param>
        /// <param name="trust">3 надежность по умолчанию</param>
		public Receive(string connectionString, string dataTable, int uslovie, int parameter, int parameter2, int trust = 3)
		{
			this.dataTable = dataTable;
			code = uslovie;
			this.parameter = parameter;
			this.parameter2 = parameter2;
			this.trust = trust;
			this.connectionString = connectionString;
		}

		public void Start()
		{
			Start(true);
		}

		public void Start(bool start)
		{
			try
			{
				if(!Started)
				{
					WaitReceiveDelegate wrd = Connectfor;
					wrd.BeginInvoke(start, null, null);
				}
			}
			catch
			{
				Exit();
			}
		}

		public KescoUdpClient ReservePort()
		{
			Exit();
			IPAddress ipListen;
			IPAddress[] ipads = Dns.GetHostEntry(Dns.GetHostName()).AddressList;
			if(ipads.Where(x => x.AddressFamily == AddressFamily.InterNetwork).Count() == 1)
			{
				ipListen = ipads.First(x => x.AddressFamily == AddressFamily.InterNetwork);
			}
			else
			{
				string ip = "";
				if(GetIpToServer.TestConnection(connectionString, out ip))
				{
					try
					{
						ipListen = IPAddress.Parse(ip);
					}
					catch(Exception ex)
					{
						throw new Exception(ip, ex);
					}
				}
				else
				{
					return null;
				}
			}
			extPoint = new IPEndPoint(ipListen, 10300);
			int i = 10300;
			while(!Trying && (i != 11300))
			{
				extPoint.Port = i;
				try
				{
					client = new KescoUdpClient(extPoint);
					Trying = true;
				}
				catch
				{
					i++;
				}
			}
			if(i == 11300)
			{
				return null;
			}
			return client;
		}

        /// <summary>
        /// Обновить подписку. Подписаться на иной документ
        /// </summary>
        /// <param name="parameter1"></param>
        /// <returns>Результат. true успех</returns>
	    public bool Update(int parameter1)
	    {
            // Проверка
            Console.WriteLine(@"{0}: CheckState Reciever", DateTime.Now.ToString("HH:mm:ss fff"));
            if (CheckState())
            {
                parameter = parameter1;

                Started = false;

                if (strID > 0)
                {
                    Console.WriteLine(@"{0}: UpdateSubscription start", DateTime.Now.ToString("HH:mm:ss fff"));
                    bool ret = UpdateSubscription(true);
                    Console.WriteLine(@"{0}: UpdateSubscription end", DateTime.Now.ToString("HH:mm:ss fff"));
                    return ret;
                }
            }
            
            return false;
	    }

	    protected virtual void OnRecived(string recievedString)
		{
			if(Received != null && !string.IsNullOrEmpty(recievedString))
			{
				try
				{
					Received(recievedString, parameter);
				}
				catch
				{
				}
			}
#if(DEBUG)
			else
				MessageBox.Show("No event\n" + recievedString);
#endif
		}

		[STAThread]
		private void Connectfor(bool start)
		{
			Thread.CurrentThread.IsBackground = true;
			if(start)
				ReservePort();
			if(client == null)
				return;
			IPAddress ipListen = extPoint.Address;
			int port = extPoint.Port;
			using(var cn = new SqlConnection(connectionString))
			using(SqlCommand scm =
					new SqlCommand("DELETE FROM " + dataTable + " WHERE (IPАдрес = @adr) AND (Порт = @port)\nINSERT INTO " + dataTable +
						" (IPАдрес, Порт, Надёжность, КодУсловия" + ((parameter != -1) ? ", КодПараметра" : "") + ((parameter2 != -1) ? ", КодПараметра2" : "") +
						") VALUES (@adr, @port, @trust, @code" + ((parameter != -1) ? ",@param" : "") + ((parameter2 != -1) ? ",@param2" : "") +
						") SELECT SCOPE_IDENTITY()", cn))
			{
				scm.Parameters.Add("@adr", SqlDbType.VarChar, 50).Value = ipListen.ToString();
				scm.Parameters.Add("@port", SqlDbType.Int, 4).Value = port;
				scm.Parameters.Add("@code", SqlDbType.Int, 4).Value = code;
				if(parameter != -1)
					scm.Parameters.Add("@param", SqlDbType.Int, 4).Value = parameter;
				if(parameter2 != -1)
					scm.Parameters.Add("@param2", SqlDbType.Int, 4).Value = parameter2;

				scm.Parameters.Add("@trust", SqlDbType.TinyInt, 1).Value = trust;
				try
				{
					scm.Connection.Open();
					using(SqlDataReader dr = scm.ExecuteReader())
					{
						if(dr.Read())
							strID = (int)((decimal)dr[0]);
						dr.Close();
					}
					Started = strID > 0;
				}
				catch(SqlException sqlEx)
				{
					sqlErrorShow(sqlEx);
				}
				finally
				{
					scm.Connection.Close();
				}
			}
			try
			{
				var instance = client;
				res = client.BeginReceive(DataReceived, new object[] { extPoint, this.client });
			}
			catch(Exception ex)
			{
				Console.WriteLine(ex.ToString());
			}
		}

		private void DataReceived(IAsyncResult ar)
		{
			if(!Started)
				return;
			var objs = (object[])ar.AsyncState;
			KescoUdpClient tmpClient = (KescoUdpClient)objs[1];
			var ipEndPoint = (IPEndPoint)objs[0];
			IPAddress IpListen = extPoint.Address;
			int port = extPoint.Port;
			try
			{
				getByte = tmpClient.EndReceive(ar, ref ipEndPoint);
			}
			catch
			{
				return;
			}
			if(tmpClient != client)
			{
				tmpClient.MyClose();
				tmpClient = null;
			}
			extPoint.Address = IpListen;
			extPoint.Port = port;
			UpdateSubscription();
			if(getByte != null)
				OnRecived(Encoding.Default.GetString(getByte));
			try
			{
				if(tmpClient != null && client != null)
					client.BeginReceive(DataReceived, ar.AsyncState);
			}
			catch
			{
			}
		}

		public void UpdateSql()
		{
			if(res != null && res.IsCompleted)
				res = client.BeginReceive(DataReceived, res.AsyncState);
			UpdateSubscription();
		}

        /// <summary>
        /// Обновить подписку
        /// </summary>
        /// <param name="updateParams"></param>
        /// <returns>Результат. true успех</returns>
		private bool UpdateSubscription(bool updateParams = false)
        {
            bool result = false;

			if(strID > 0)
			{
			    var commandText = "UPDATE " + dataTable + " SET Надёжность = @trust "
                    + (updateParams && parameter != -1 ? ", КодПараметра = @param" : String.Empty)
                    +  " WHERE (КодРегистрацииРассылки = @id) SELECT @@ROWCOUNT";

				using(SqlConnection cn = new SqlConnection(connectionString))
                using (SqlCommand scm = new SqlCommand(commandText, cn))
				{
					scm.Parameters.Add("@id", SqlDbType.Int, 4).Value = strID;
					scm.Parameters.Add("@trust", SqlDbType.TinyInt, 1).Value = trust;

                    // Обновля рассылку с изменением параметра
				    if (updateParams && parameter != -1)
				        scm.Parameters.Add("@param", SqlDbType.Int, 4).Value = parameter;

				    try
					{
						scm.Connection.Open();
						SqlDataReader dr = scm.ExecuteReader();
						if(dr.Read())
						{
							if((int)dr[0] == 0)
							{
								dr.Close();
								dr.Dispose();
								scm.Connection.Close();
								scm.Parameters.Clear();
								scm.CommandText = "INSERT INTO " + dataTable +
						" (IPАдрес, Порт, Надёжность, КодУсловия" + ((parameter != -1) ? ", КодПараметра" : "") + ((parameter2 != -1) ? ", КодПараметра2" : "") +
						") VALUES (@adr, @port, @trust, @code" + ((parameter != -1) ? ",@param" : "") + ((parameter2 != -1) ? ",@param2" : "") +
						") SELECT SCOPE_IDENTITY()";
								scm.Parameters.Add("@adr", SqlDbType.VarChar, 50).Value =
									extPoint.Address.ToString();
								scm.Parameters.Add("@port", SqlDbType.Int, 4).Value = extPoint.Port;
								scm.Parameters.Add("@code", SqlDbType.Int, 4).Value = code;
								if(parameter != -1)
									scm.Parameters.Add("@param", SqlDbType.Int, 4).Value = parameter;
								if(parameter2 != -1)
									scm.Parameters.Add("@param2", SqlDbType.Int, 4).Value = parameter2;
								scm.Parameters.Add("@trust", SqlDbType.TinyInt, 1).Value = trust;
								scm.Connection.Open();
								using(dr = scm.ExecuteReader())
									if(dr.Read())
										strID = (int)((decimal)dr[0]);
							}
							dr.Close();
							dr.Dispose();
						}
						Started = true;

					    result = true;
					}
					catch(SqlException sqlEx)
					{
						sqlErrorShow(sqlEx);
					}
					finally
					{
						cn.Close();
					}
				}
			}

            return result;
        }

		public int GetParameter()
		{
			return parameter;
		}

		public int GetParameter2()
		{
			return parameter2;
		}

		public int GetCondition()
		{
			return code;
		}

		public int GetSuscribe()
		{
			return strID;
		}

		public void Exit()
		{
			try
			{
				Started = false;
				if(client != null)
				{
					client.MyClose();
					client = null;
				}
				if(strID > 0)
					using(var cn = new SqlConnection(connectionString))
					using(SqlCommand scm = new SqlCommand("DELETE FROM " + dataTable + " WHERE (КодРегистрацииРассылки = @id)", cn))
					{
						scm.Parameters.Add("@id", SqlDbType.Int, 4).Value = strID;
						try
						{
							scm.Connection.Open();
							scm.ExecuteNonQuery();
						}
						catch(SqlException sqlEx)
						{
							sqlErrorShow(sqlEx);
						}
						finally
						{
							scm.Connection.Close();
						}
					}
			}
			catch
			{
			}
		}

		private void sqlErrorShow(SqlException sqlEx)
		{
			if(sqlEx.Number == 229)
				MessageBox.Show(
					"У вас недостаточно прав для выполнения этой операции." + Environment.NewLine +
					Environment.NewLine + sqlEx.Message, "Ошибка: Отказано в доступе");
			else if(sqlEx.Number == 6005 || sqlEx.Number == 10054)
			{
				MessageBox.Show("Сервер перезапускается.\nС Архивом можно будет работать через одну-две минуты",
								"Ошибка: Отказано в доступе");
			}
			else if(sqlEx.Number == 11 || sqlEx.Number == 2 || sqlEx.Number == 64)
				MessageBox.Show("Сервер не доступен.", "Ошибка: нет доступа");
			else
				MessageBox.Show(sqlEx.Message, "Ошибка доступа к базе данных ");
		}

        /// <summary>
        /// Проверка UDP клиента
        /// </summary>
        /// <returns></returns>
	    private bool CheckState()
	    {
            // TODO Проверка, например посылкой тестового UDP сообщения через вызов sp сервера
            // Как вариант, можно периодически создавать новый экземпляр

	        bool result = false;

            try
            {
                result = client != null && client.Available > -1;
            }
            catch (SocketException)
            {

            }
            catch (Exception)
            {

            }

	        return result;
	    }

	    #region IDisposable Members

		public void Dispose()
		{
			if(Received != null)
				Received = null;
			if(Started)
				Exit();
		}

		#endregion
	}
}