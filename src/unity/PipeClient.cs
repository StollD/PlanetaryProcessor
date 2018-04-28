using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using PlanetaryProcessor.Unity;
using UnityEngine;
using Logger = Kopernicus.Logger;

namespace PlanetaryProcessor.Unity
{
    public class PipeClient : IDisposable
    {
        /// <summary>
        /// The pipe that writes to the controller
        /// </summary>
        private TcpClient _client;

        /// <summary>
        /// Whether the Pipe was disposed
        /// </summary>
        private Boolean _isDisposed;

        /// <summary>
        /// The messages that are read by the callbacks
        /// </summary>
        private Dictionary<String, Queue<String>> _messages;
        
        public PipeClient(Int32 port)
        {
            _client = new TcpClient(new IPEndPoint(IPAddress.Loopback, port).AddressFamily);
            _client.Connect(IPAddress.Loopback, port);
            _client.Client.NoDelay = true;
            _client.Client.DontFragment = true;
            while (!_client.Connected)
            {
                Thread.Sleep(100);
            }
            _messages = new Dictionary<String, Queue<String>>();

            Entrypoint.Instance.StartCoroutine(CheckForNewMessages());
        }

        private IEnumerator<WaitForSecondsRealtime> CheckForNewMessages()
        {
            while (!_isDisposed)
            {
                String s = ReadMessage(_client.Client);
                String[] split = s.Split(new[] {"::"}, StringSplitOptions.RemoveEmptyEntries);
                if (split.Length == 2)
                {
                    if (!_messages.ContainsKey(split[0]))
                    {
                        _messages.Add(split[0], new Queue<String>());
                    }

                    _messages[split[0]].Enqueue(split[1]);
                }

                yield return null;
            }
        }

        /// <summary>
        /// Gets new messages that were sent with the specified ident
        /// </summary>
        public void ReadMessage(String ident, Action<String> callback)
        {
            Entrypoint.Instance.StartCoroutine(ReadMessageInternal(ident, callback));
        }

        private IEnumerator ReadMessageInternal(String ident, Action<String> callback)
        {
            while (!_isDisposed)
            {
                if (_messages.ContainsKey(ident) && _messages[ident].Any())
                {
                    callback(_messages[ident].Dequeue());
                }

                yield return null;
            }

            callback(null);
        }

        /// <summary>
        /// Sends a message to the programs connected to this pipe
        /// </summary>
        public void SendMessage(String ident, String message)
        {
            String fullMsg = ident + "::" + message;
            SendMessage(_client.Client, fullMsg);
        }
        
        private static void SendMessage(Socket socket, String msg)
        {
            Byte[] data = Encoding.ASCII.GetBytes(msg);
            Byte[] sizeinfo = new Byte[4];

            //could optionally call BitConverter.GetBytes(data.length);
            sizeinfo[0] = (Byte)data.Length;
            sizeinfo[1] = (Byte)(data.Length >> 8);
            sizeinfo[2] = (Byte)(data.Length >> 16);
            sizeinfo[3] = (Byte)(data.Length >> 24);

            socket.Send(sizeinfo);
            socket.Send(data);
        }
        
        private static String ReadMessage(Socket socket)
        {
            Byte[] sizeinfo = new Byte[4];

            //read the size of the message
            Int32 totalread = 0, currentread = 0;

            currentread = totalread = socket.Receive(sizeinfo);

            while (totalread < sizeinfo.Length && currentread > 0)
            {
                currentread = socket.Receive(sizeinfo,
                          totalread, //offset into the buffer
                          sizeinfo.Length - totalread, //max amount to read
                          SocketFlags.None);

                totalread += currentread;
            }

            Int32 messagesize = 0;

            //could optionally call BitConverter.ToInt32(sizeinfo, 0);
            messagesize |= sizeinfo[0];
            messagesize |= (((Int32)sizeinfo[1]) << 8);
            messagesize |= (((Int32)sizeinfo[2]) << 16);
            messagesize |= (((Int32)sizeinfo[3]) << 24);
           
            //create a byte array of the correct size
            //note:  there really should be a size restriction on
            //              messagesize because a user could send
            //              Int32.MaxValue and cause an OutOfMemoryException
            //              on the receiving side.  maybe consider using a short instead
            //              or just limit the size to some reasonable value
            Byte[] data = new Byte[messagesize];

            //read the first chunk of data
            totalread = 0;
            currentread = totalread = socket.Receive(data,
                         totalread, //offset into the buffer
                        data.Length - totalread, //max amount to read
                        SocketFlags.None);

            //if we didn't get the entire message, read some more until we do
            while (totalread < messagesize && currentread > 0)
            {
                currentread = socket.Receive(data,
                         totalread, //offset into the buffer
                        data.Length - totalread, //max amount to read
                        SocketFlags.None);
                totalread += currentread;
            }

            return Encoding.ASCII.GetString(data, 0, totalread);                   
        }

        public void Dispose()
        {
            _client.GetStream().Close();
            _client.Close();
            _isDisposed = true;
        }
    }
}