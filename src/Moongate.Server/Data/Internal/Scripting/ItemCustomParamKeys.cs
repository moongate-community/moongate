namespace Moongate.Server.Data.Internal.Scripting;

public static class ItemCustomParamKeys
{
    public static class Book
    {
        public const string BookId = "book_id";
        public const string Title = "book_title";
        public const string Author = "book_author";
        public const string Content = "book_content";
        public const string Pages = "book_pages";
        public const string Writable = "book_writable";
    }

    public static class Door
    {
        public const string Facing = "door_facing";
        public const string LinkSerial = "door_link_serial";
    }

    public static class Spawner
    {
        public const string SpawnerId = "spawner_id";
    }
}
