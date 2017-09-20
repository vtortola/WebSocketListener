namespace vtortola.WebSockets
{
    public interface IBufferManager
    {
        void Clear();
        void ReturnBuffer(byte[] buffer);
        byte[] TakeBuffer(int bufferSize);
    }
}
