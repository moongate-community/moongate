namespace Moongate.Core.Collections;

public static class CollectionThrowStrings
{
    public const string ArgumentOutOfRange_Index =
        "Index was out of range. Must be non-negative and less than the size of the collection.";

    public const string ArgumentOutOfRange_NeedNonNegNum = "Non-negative number required.";

    public const string Argument_InvalidOffLen =
        "Offset and length were out of bounds for the array or count is greater than the number of elements from index to the end of the source collection.";

    public const string Argument_AddingDuplicate = "An item with the same value has already been added. Value: {0}";

    public const string Arg_ArrayPlusOffTooSmall =
        "Destination array is not long enough to copy all the items in the collection. Check array index and length.";

    public const string InvalidOperation_ConcurrentOperationsNotSupported =
        "Operations that change non-concurrent collections must have exclusive access. A concurrent update was performed on this collection and corrupted its state. The collection's state is no longer correct.";

    public const string InvalidOperation_EnumFailedVersion =
        "Collection was modified after the enumerator was instantiated.";

    public const string InvalidOperation_EmptyQueue = "Queue empty.";

    public const string InvalidOperation_EnumNotStarted = "Enumeration has not started. Call MoveNext.";

    public const string InvalidOperation_EnumEnded = "Enumeration already finished.";

    public const string Argument_ArrayTooLarge =
        "The input array length must not exceed Int32.MaxValue / {0}. Otherwise BitArray.Length would exceed Int32.MaxValue.";

    public const string Arg_ArrayLengthsDiffer = "Array lengths must be the same.";

    public const string Arg_RankMultiDimNotSupported =
        "Only single dimensional arrays are supported for the requested action.";

    public const string Arg_BitArrayTypeUnsupported =
        "Only supported array types for CopyTo on BitArrays are Boolean[], Int32[] and Byte[].";
}
