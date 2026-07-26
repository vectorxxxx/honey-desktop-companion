namespace Honey.Domain.Movement;

public static class PetLocomotionEngine
{
    public static LocomotionFrame Step(
        LocomotionState state,
        PetLocomotionInput input,
        PetLocomotionProfile profile,
        TimeSpan elapsed)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(profile);
        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed));
        }
        if (!input.Bounds.IsValid)
        {
            throw new ArgumentException("运动边界无效。", nameof(input));
        }

        var safeProfile = profile.Normalize();
        var step = elapsed > safeProfile.MaximumStep ? safeProfile.MaximumStep : elapsed;
        var target = input.Bounds.Clamp(input.Target);
        var delta = target - state.Position;
        var distance = delta.Length;
        var shouldStop = input.Intent is LocomotionIntent.Idle or LocomotionIntent.Anchor
            || distance <= safeProfile.ArrivalRadius;
        if (shouldStop || step == TimeSpan.Zero)
        {
            var stopped = state with
            {
                Velocity = LocomotionPoint.Zero,
                TurnLean = 0
            };
            return new LocomotionFrame(stopped, 0, distance <= safeProfile.ArrivalRadius);
        }

        var seconds = step.TotalSeconds;
        var desiredDirection = delta.Normalize();
        var currentFacing = state.Facing.Normalize();
        if (currentFacing == LocomotionPoint.Zero)
        {
            currentFacing = desiredDirection;
        }

        var maxTurn = safeProfile.MaxTurnRadiansPerSecond * seconds;
        var (facing, appliedTurn) = RotateTowards(currentFacing, desiredDirection, maxTurn);
        var modeMultiplier = input.IsBerserk ? safeProfile.BerserkSpeedMultiplier : 1;
        var speedMultiplier = Math.Max(0, input.SpeedMultiplier) * modeMultiplier;
        var maximumSpeed = safeProfile.MaxSpeed * speedMultiplier;
        var arrivalScale = Math.Clamp(distance / safeProfile.DecelerationRadius, 0, 1);
        var desiredVelocity = facing * (maximumSpeed * arrivalScale);
        var acceleration = safeProfile.Acceleration * modeMultiplier * seconds;
        var velocity = MoveTowards(state.Velocity, desiredVelocity, acceleration);
        var rawPosition = state.Position + velocity * seconds;
        var position = input.Bounds.Clamp(rawPosition);
        if (position.X != rawPosition.X)
        {
            velocity = velocity with { X = 0 };
        }
        if (position.Y != rawPosition.Y)
        {
            velocity = velocity with { Y = 0 };
        }

        var speed = velocity.Length;
        var animationReferenceSpeed =
            safeProfile.MaxSpeed * Math.Max(0, input.SpeedMultiplier);
        var normalizedSpeed = animationReferenceSpeed <= double.Epsilon
            ? 0
            : Math.Clamp(speed / animationReferenceSpeed, 0, 1);
        var stride = (state.StridePhase + normalizedSpeed * seconds * 4) % 1;
        var turnLean = maxTurn <= double.Epsilon
            ? 0
            : Math.Clamp(appliedTurn / maxTurn, -1, 1);
        var next = state with
        {
            Position = position,
            Velocity = velocity,
            Facing = facing,
            StridePhase = stride,
            TurnLean = turnLean
        };
        return new LocomotionFrame(next, normalizedSpeed, false);
    }

    private static LocomotionPoint MoveTowards(
        LocomotionPoint current,
        LocomotionPoint target,
        double maximumDelta)
    {
        var delta = target - current;
        var distance = delta.Length;
        return distance <= maximumDelta || distance <= double.Epsilon
            ? target
            : current + delta / distance * maximumDelta;
    }

    private static (LocomotionPoint Facing, double AppliedTurn) RotateTowards(
        LocomotionPoint current,
        LocomotionPoint target,
        double maximumTurn)
    {
        var cross = current.X * target.Y - current.Y * target.X;
        var dot = Math.Clamp(LocomotionPoint.Dot(current, target), -1, 1);
        var angle = Math.Atan2(cross, dot);
        var applied = Math.Clamp(angle, -maximumTurn, maximumTurn);
        var cosine = Math.Cos(applied);
        var sine = Math.Sin(applied);
        return (
            new LocomotionPoint(
                current.X * cosine - current.Y * sine,
                current.X * sine + current.Y * cosine).Normalize(),
            applied);
    }
}
