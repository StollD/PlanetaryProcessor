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
                Logger.Default.Log(s);
                String[] split = s.Split(new[] {"::"}, StringSplitOptions.RemoveEmptyEntries);
                if (split.Length == 2)
                {
                    if (!_messages.ContainsKey(split[0]))
                    {
                        _messages.Add(split[0], new Queue<String>());
                    }

                    _messages[split[0]].Enqueue(split[1]);
                }
                
                SendMessage("KEEPALIVE", "HHH");

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

        private Byte[] _buffer;
        
        private String ReadString()
        {
            _buffer = new Byte[4];
            _client.Read(_buffer, 0, 4);
            Int32 len = BitConverter.ToInt32(_buffer, 0);
            _buffer = new Byte[len];
            _client.Read(_buffer, 0, len);

            return Encoding.Unicode.GetString(_buffer);
        }

        private Int32 WriteString(String outString)
        {
            _buffer = Encoding.Unicode.GetBytes(outString);
            Int32 len = _buffer.Length;
            _server.Write(BitConverter.GetBytes(len), 0, 4);
            _server.Write(_buffer, 0, len);
            _server.Flush();

            return _buffer.Length + 4;
        }

        public void Dispose()
        {
            _isDisposed = true;
            _client.Close();
            _server.Close();
        }
    }
}