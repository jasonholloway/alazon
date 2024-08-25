namespace Alazon.Text;

public record Split(Split? Previous, Readable Readable)
{
    public override string ToString() => Readable.ToString();
}
