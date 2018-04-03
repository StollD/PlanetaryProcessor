using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlanetaryProcessor
{
    public class PipeServer : IDisposable
    {
        /// <summary>
        /// The pipe that reads from the other app
        /// </summary>
        private NamedPipeClientStream _client;
        
        /// <summary>
        /// The pipe thats writes to the other app
        /// </summary>
        private NamedPipeServerStream _server;

        /// <summary>
        /// Whether the Pipe was disposed
        /// </summary>
        private Boolean _isDisposed;

        /// <summary>
        /// The callbacks that respond to certain messages
        /// </summary>
        private Dictionary<String, Queue<String>> _messages;

        /// <summary>
        /// The name of the pipe
        /// </summary>
        private String _name;
        
        public PipeServer(String name)
        {
            _name = name;
            _server = new NamedPipeServerStream(name, PipeDirection.InOut);
            _messages = new Dictionary<String, Queue<String>>();
        }

        private async Task CheckForNewMessages()
        {
            String s = await ReadString();
            String[] split = s.Split(new[] {"::"}, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length > 2)
            {
                // Ignore the message
                return;
            }

            if (!_messages.ContainsKey(split[0]))
            {
                _messages.Add(split[0], new Queue<String>());
            }
            _messages[split[0]].Enqueue(split[1]);
        }

        /// <summary>
        /// Gets new messages that were sent with the specified ident
        /// </summary>
        public async Task<String> ReadMessage(String ident)
        {
            return await Task.Run(() =>
            {
                while (!_isDisposed)
                {
                    if (!_messages.ContainsKey(ident))
                    {
                        continue;
                    }

                    if (_messages[ident].Any())
                    {
                        return _messages[ident].Dequeue();
                    }
                }

                return null;
            });
        }

        /// <summary>
        /// Sends a message to the programs connected to this pipe
        /// </summary>
        public async Task SendMessage(String ident, String message)
        {
            String fullMsg = ident + "::" + message;
            await WriteString(fullMsg);
        }
        
        private Byte[] _buffer;

        private async Task<String> ReadString()
        {
            _buffer = new Byte[4];
            await _client.ReadAsync(_buffer, 0, 4);
            Int32 len = BitConverter.ToInt32(_buffer, 0);
            _buffer = new Byte[len];
            await _client.ReadAsync(_buffer, 0, len);

            return Encoding.Unicode.GetString(_buffer);
        }

        private async Task<Int32> WriteString(String outString)
        {
            _buffer = Encoding.Unicode.GetBytes(outString);
            Int32 len = _buffer.Length;
            await _server.WriteAsync(BitConverter.GetBytes(len), 0, 4);
            await _server.WriteAsync(_buffer, 0, len);
            _server.Flush();

            return _buffer.Length + 4;
        }

        public async Task WaitForConnection()
        {
            await Task.Run(() => { _server.WaitForConnection(); });

            // Connect to the other direction
            _client = new NamedPipeClientStream(".", _name + "-RE", PipeDirection.InOut);
            _client.Connect();
            
            Task.Run(async () =>
            {
                while (!_isDisposed)
                {
                    await CheckForNewMessages();
                    await Task.Delay(100);
                }
            });
        }

        public void Dispose()
        {
            _isDisposed = true;
            _client.Close();
            _server.Close();
        }
    }
}