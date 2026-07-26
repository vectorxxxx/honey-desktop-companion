using Honey.Domain.Activity;

namespace Honey.Desktop.Runtime;

public sealed class PetActivityJournal
{
    private readonly int _capacity;
    private readonly List<PetActivityEntry> _entries = [];

    public PetActivityJournal(int capacity = 20)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _capacity = capacity;
    }

    public IReadOnlyList<PetActivityEntry> Entries => _entries;

    public void Append(PetActivityEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _entries.Insert(0, entry);
        if (_entries.Count > _capacity)
        {
            _entries.RemoveRange(_capacity, _entries.Count - _capacity);
        }
    }
}
