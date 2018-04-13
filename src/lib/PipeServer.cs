using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
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
        /// The messages that are read by the callbacks
        /// </summary>
        private Dictionary<String, Queue<String>> _messages;

        /// <summary>
        /// The name of the pipe
        /// </summary>
        private String _name;

        /// <summary>
        /// An object that makes sure certain code isn't executed at the same time
        /// </summary>
        private Object _lock;
        
        public PipeServer(String name)
        {
            _name = name;
            _server = new NamedPipeServerStream(name, PipeDirection.InOut);
            _messages = new Dictionary<String, Queue<String>>();
            _lock = new Object();
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

            lock (_lock)
            {
                if (!_messages.ContainsKey(split[0]))
                {
                    _messages.Add(split[0], new Queue<String>());
                }

                _messages[split[0]].Enqueue(split[1]);
            }
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
                    lock (_lock)
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

        private async Task<String> ReadString()
        {
            Byte[] sizeinfo = new Byte[4];

            // Read the size of the message
            Int32 totalread = 0, currentread = 0;

            currentread = totalread = await _client.ReadAsync(sizeinfo, 0, 4);

            while (totalread < sizeinfo.Length && currentread > 0)
            {
                currentread = await _client.ReadAsync(sizeinfo,
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
            currentread = totalread = await _client.ReadAsync(data,
                totalread, //offset into the buffer
                data.Length - totalread //max amount to read
            );

            //if we didn't get the entire message, read some more until we do
            while (totalread < messagesize && currentread > 0)
            {
                currentread = await _client.ReadAsync(data,
                    totalread, //offset into the buffer
                    data.Length - totalread //max amount to read
                );
                totalread += currentread;
            }

            return Encoding.ASCII.GetString(data, 0, totalread);
        }

        private async Task<Int32> WriteString(String outString)
        {
            Byte[] data = Encoding.ASCII.GetBytes(outString);
            Byte[] sizeinfo = new Byte[4];

            //could optionally call BitConverter.GetBytes(data.length);
            sizeinfo[0] = (Byte)data.Length;
            sizeinfo[1] = (Byte)(data.Length >> 8);
            sizeinfo[2] = (Byte)(data.Length >> 16);
            sizeinfo[3] = (Byte)(data.Length >> 24);

            await _server.WriteAsync(sizeinfo, 0, 4);
            await _server.WriteAsync(data, 0, data.Length);
            _server.Flush();

            return data.Length + 4;
        }

        public async Task WaitForConnection()
        {
            await Task.Run(() => { _server.WaitForConnection(); });
            await Task.Delay(200);

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