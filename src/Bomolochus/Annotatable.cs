namespace Bomolochus;

public interface Annotatable
{
    void Add(Addenda addenda);
    Addenda Extract();
}
