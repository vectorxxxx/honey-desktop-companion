using Honey.Domain.Model;

namespace Honey.Domain.Events;

public interface IDomainEvent;

public sealed record PetModeChanged(
    Guid PetId,
    PetMode Before,
    PetMode After) : IDomainEvent;

public sealed record BehaviorSelected(
    Guid PetId,
    string Behavior) : IDomainEvent;

public sealed record PetInteractionOccurred(
    Guid PetId,
    string Kind) : IDomainEvent;
