using ComicSort.Services.Interfaces;

namespace ComicSort.Services
{
    public class MessageService : IMessageService
    {
        public string GetMessage()
        {
            return "Hello from ComicSort";
        }
    }
}
