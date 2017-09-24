namespace vtortola.WebSockets
{
    public interface IBufferManager
    {
        void ReturnBuffer(byte[] buffer);
        byte[] TakeBuffer(int bufferSize);
    }
}
