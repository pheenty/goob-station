// SPDX-FileCopyrightText: 2024 Piras314 <p1r4s@proton.me>
// SPDX-FileCopyrightText: 2024 username <113782077+whateverusername0@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 whateverusername0 <whateveremail>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <aiden@djkraz.com>
// SPDX-FileCopyrightText: 2025 Aviu00 <93730715+Aviu00@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aviu00 <aviu00@protonmail.com>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Misandry <mary@thughunt.ing>
// SPDX-FileCopyrightText: 2025 gluesniffler <159397573+gluesniffler@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 gus <august.eymann@gmail.com>
// SPDX-FileCopyrightText: 2025 pheenty <fedorlukin2006@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.Bloodstream;
using Content.Server.Heretic.Components.PathSpecific;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Damage.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components; // Shitmed Change
namespace Content.Server.Heretic.EntitySystems.PathSpecific;

public sealed class ChampionStanceSystem : EntitySystem
{
    [Dependency] private readonly MobThresholdSystem _threshold = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeedModifierSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChampionStanceComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<ChampionStanceComponent, BeforeStaminaDamageEvent>(OnBeforeStaminaDamage);
        SubscribeLocalEvent<ChampionStanceComponent, GetBloodlossDamageMultiplierEvent>(OnGetBloodlossMultiplier);
        SubscribeLocalEvent<ChampionStanceComponent, ComponentStartup>(OnChampionStartup);
        SubscribeLocalEvent<ChampionStanceComponent, ComponentShutdown>(OnChampionShutdown);
        SubscribeLocalEvent<ChampionStanceComponent, ModifySlowOnDamageSpeedEvent>(OnChampionModifySpeed);

        // if anyone is reading through and does not have EE newmed you can remove these handlers
        SubscribeLocalEvent<ChampionStanceComponent, BodyPartAddedEvent>(OnBodyPartAdded);
        SubscribeLocalEvent<ChampionStanceComponent, BodyPartRemovedEvent>(OnBodyPartRemoved);
    }

    private void OnChampionModifySpeed(Entity<ChampionStanceComponent> ent, ref ModifySlowOnDamageSpeedEvent args)
    {
        var dif = 1f - args.Speed;
        if (dif <= 0f)
            return;

        // reduces the slowness modifier by the given coefficient
        args.Speed += dif * 0.5f;
    }

    private void OnChampionShutdown(Entity<ChampionStanceComponent> ent, ref ComponentShutdown args)
    {
        _movementSpeedModifierSystem.RefreshMovementSpeedModifiers(ent);
    }

    private void OnChampionStartup(Entity<ChampionStanceComponent> ent, ref ComponentStartup args)
    {
        _movementSpeedModifierSystem.RefreshMovementSpeedModifiers(ent);
    }

    private void OnGetBloodlossMultiplier(Entity<ChampionStanceComponent> ent,
        ref GetBloodlossDamageMultiplierEvent args)
    {
        args.Multiplier *= 0.5f;
    }

    public bool Condition(Entity<ChampionStanceComponent> ent)
    {
        if (!TryComp(ent, out DamageableComponent? dmg) || !TryComp(ent, out MobThresholdsComponent? thresholdComp))
            return false;

        if (!_threshold.TryGetThresholdForState(ent, MobState.Critical, out var threshold, thresholdComp))
            threshold = _threshold.GetThresholdForState(ent, MobState.Dead, thresholdComp);
        return dmg.TotalDamage >= threshold.Value.Float() / 2f;
    }

    private void OnDamageModify(Entity<ChampionStanceComponent> ent, ref DamageModifyEvent args)
    {
        if (!Condition(ent))
            return;

        args.Damage = args.OriginalDamage / 2f;
    }

    private void OnBeforeStaminaDamage(Entity<ChampionStanceComponent> ent, ref BeforeStaminaDamageEvent args)
    {
        if (!Condition(ent))
            return;

        args.Value *= 0.4f;
    }

    private void OnBodyPartAdded(Entity<ChampionStanceComponent> ent, ref BodyPartAddedEvent args)
    {
        // can't touch this
        if (!TryComp(args.Part, out WoundableComponent? woundable))
            return;

        woundable.CanRemove = false;
        Dirty(args.Part);
    }
    private void OnBodyPartRemoved(Entity<ChampionStanceComponent> ent, ref BodyPartRemovedEvent args)
    {
        // can touch this
        if (!TryComp(args.Part, out WoundableComponent? woundable))
            return;

        woundable.CanRemove = true;
        Dirty(args.Part);
    }
}
