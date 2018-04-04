using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
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
        /// The pipe that reads from the controller
        /// </summary>
        private NamedPipeClientStream _client;

        /// <summary>
        /// The pipe that writes to the controller
        /// </summary>
        private NamedPipeServerStream _server;

        /// <summary>
        /// Whether the Pipe was disposed
        /// </summary>
        private Boolean _isDisposed;

        /// <summary>
        /// The messages that are read by the callbacks
        /// </summary>
        private Dictionary<String, Queue<String>> _messages;
        
        public PipeClient(String name)
        {
            _client = new NamedPipeClientStream(".", name, PipeDirection.InOut);
            _client.Connect();

            while (!_client.IsConnected)
            {
                Thread.Sleep(100);
            }
            
            _server = new NamedPipeServerStream(name + "-RE", PipeDirection.InOut);
            _server.WaitForConnection();
            _messages = new Dictionary<String, Queue<String>>();

            Entrypoint.Instance.StartCoroutine(CheckForNewMessages());
        }

        private IEnumerator<WaitForSecondsRealtime> CheckForNewMessages()
        {
            while (!_isDisposed)
            {
                String s = ReadString();
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
            WriteString(fullMsg);
        }
        
        private String ReadString()
        {
            Byte[] sizeinfo = new Byte[4];

            // Read the size of the message
            Int32 totalread = 0, currentread = 0;

            currentread = totalread = _client.Read(sizeinfo, 0, 4);

            while (totalread < sizeinfo.Length && currentread > 0)
            {
                currentread = _client.Read(sizeinfo,
                    totalread, //offset into the buffer
                    sizeinfo.Length - totalread //max amount to read
                );

                totalread += currentread;
            }

            Int32 messagesize = 0;

            //could optionally call BitConverter.ToInt32(sizeinfo, 0);
            messagesize |= sizeinfo[0];
            messagesize |= sizeinfo[1] << 8;
            messagesize |= sizeinfo[2] << 16;
            messagesize |= sizeinfo[3] << 24;

            //create a byte array of the correct size
            //note:  there really should be a size restriction on
            //              messagesize because a user could send
            //              Int32.MaxValue and cause an OutOfMemoryException
            //              on the receiving side.  maybe consider using a short instead
            //              or just limit the size to some reasonable value
            Byte[] data = new Byte[messagesize];

            //read the first chunk of data
            totalread = 0;
            currentread = totalread = _client.Read(data,
                totalread, //offset into the buffer
                data.Length - totalread //max amount to read
            );

            //if we didn't get the entire message, read some more until we do
            while (totalread < messagesize && currentread > 0)
            {
                currentread = _client.Read(data,
                    totalread, //offset into the buffer
                    data.Length - totalread //max amount to read
                );
                totalread += currentread;
            }

            return Encoding.ASCII.GetString(data, 0, totalread);
        }

        private Int32 WriteString(String outString)
        {
            Byte[] data = Encoding.ASCII.GetBytes(outString);
            Byte[] sizeinfo = new Byte[4];

            //could optionally call BitConverter.GetBytes(data.length);
            sizeinfo[0] = (Byte)data.Length;
            sizeinfo[1] = (Byte)(data.Length >> 8);
            sizeinfo[2] = (Byte)(data.Length >> 16);
            sizeinfo[3] = (Byte)(data.Length >> 24);

            _server.Write(sizeinfo, 0, 4);
            _server.Write(data, 0, data.Length);
            _server.Flush();

            return data.Length + 4;
        }

        public void Dispose()
        {
            _isDisposed = true;
            _client.Close();
            _server.Close();
        }
    }
}