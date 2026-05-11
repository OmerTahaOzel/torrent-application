using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Torrent.Core;
using Torrent.Models;

namespace Torrent.Network
{
    public class P2PMessageQueue
    {
        private readonly ConcurrentQueue<P2PMessage> _queue = new ConcurrentQueue<P2PMessage>();
        private bool _isProcessing;

        // Patronu buraya tanımlıyoruz
        private readonly TorrentEngine _engine;

        // İşçi doğduğunda patronun kim olduğunu bilecek
        public P2PMessageQueue(TorrentEngine engine)
        {
            _engine = engine;
        }

        public void EnqueueMessage(P2PMessage message)
        {
            _queue.Enqueue(message);
        }

        public void StartProcessing()
        {
            _isProcessing = true;
            Task.Run(() => ProcessQueueAsync());
        }

        private async Task ProcessQueueAsync()
        {
            while (_isProcessing)
            {
                if (_queue.TryDequeue(out P2PMessage? message) && message != null)
                {
                    _engine.ProcessMessage(message, message.SenderIp);

                    await Task.Delay(10);
                }
                else
                {
                    await Task.Delay(100);
                }
            }
        }

        public void Stop()
        {
            _isProcessing = false;
        }
    }
}
