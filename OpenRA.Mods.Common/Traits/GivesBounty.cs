#region Copyright & License Information
/*
 * Copyright 2007-2018 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Effects;
using OpenRA.Mods.Common.Warheads;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("When killed, this actor causes the attacking player to receive money.")]
	class GivesBountyInfo : ConditionalTraitInfo
	{
		[Desc("Percentage of the killed actor's Cost or CustomSellValue to be given.")]
		public readonly int Percentage = 10;

		[Desc("Stance the attacking player needs to receive the bounty.")]
		public readonly Stance ValidStances = Stance.Neutral | Stance.Enemy;

		[Desc("Whether to show a floating text announcing the won bounty.")]
		public readonly bool ShowBounty = true;

		[Desc("DeathTypes for which a bounty should be granted.",
			"Use an empty list (the default) to allow all DeathTypes.")]
		public readonly HashSet<string> DeathTypes = new HashSet<string>();

		public override object Create(ActorInitializer init) { return new GivesBounty(this); }
	}

	class GivesBounty : ConditionalTrait<GivesBountyInfo>, INotifyKilled
	{
		Cargo cargo;

		public GivesBounty(GivesBountyInfo info)
			: base(info) { }

		protected override void Created(Actor self)
		{
			base.Created(self);

			cargo = self.TraitOrDefault<Cargo>();
		}

		int GetBountyValue(Actor self)
		{
			return self.GetSellValue() * Info.Percentage / 100;
		}

		int GetDisplayedBountyValue(Actor self)
		{
			var bounty = GetBountyValue(self);
			if (cargo == null)
				return bounty;

			foreach (var a in cargo.Passengers)
			{
				var givesBounties = a.TraitsImplementing<GivesBounty>().Where(gb => !gb.IsTraitDisabled);
				foreach (var givesBounty in givesBounties)
					bounty += givesBounty.GetDisplayedBountyValue(a);
			}

			return bounty;
		}

		void INotifyKilled.Killed(Actor self, AttackInfo e)
		{
			if (e.Attacker == null || e.Attacker.Disposed || IsTraitDisabled)
				return;

			if (!Info.ValidStances.HasStance(e.Attacker.Owner.Stances[self.Owner]))
				return;

			if (Info.DeathTypes.Count > 0 && !e.Damage.DamageTypes.Overlaps(Info.DeathTypes))
				return;

			var displayedBounty = GetDisplayedBountyValue(self);
			if (Info.ShowBounty && self.IsInWorld && displayedBounty != 0 && e.Attacker.Owner.IsAlliedWith(self.World.RenderPlayer))
				e.Attacker.World.AddFrameEndTask(w => w.Add(new FloatingText(self.CenterPosition, e.Attacker.Owner.Color.RGB, FloatingText.FormatCashTick(displayedBounty), 30)));

			e.Attacker.Owner.PlayerActor.Trait<PlayerResources>().ChangeCash(GetBountyValue(self));
		}
	}
}
