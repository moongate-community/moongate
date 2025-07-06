using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.UO.Data.Events.Contexts;

public class UoItemContext
{
    public UOItemEntity Item { get; set; }
    public UOMobileEntity Mobile { get; set; }


    public void Remove()
    {

    }

}
