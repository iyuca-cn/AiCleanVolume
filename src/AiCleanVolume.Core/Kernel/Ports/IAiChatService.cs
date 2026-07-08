using System.Collections.Generic;
using AiCleanVolume.Core.Domain.Ai;
using AiCleanVolume.Core.Domain.Settings;

namespace AiCleanVolume.Core.Kernel.Ports
{
    /// <summary>多轮对话补全。同步阻塞，调用方自行放到后台线程。</summary>
    public interface IAiChatService
    {
        AiChatResult Complete(IList<AiChatMessage> messages, ApplicationSettings settings);
    }
}
