﻿using Terraria.ID;
using Terraria.ModLoader;

namespace MagicStorage.Stations
{
	//Overwrite the base class logic
	[Autoload(true)]
	public class CombinedFurnitureStations1Item : CombinedStationsItem<CombinedFurnitureStations1Tile>
	{
		public override string ItemName => "Combined Furniture Stations (Tier 1)";

		public override string ItemDescription => "Combines the functionality of several crafting stations for furniture";

		public override int Rarity => ItemRarityID.Green;

		public override void SafeSetDefaults()
		{
			Item.value = BasePriceFromItems((ItemID.BoneWelder, 1),
				(ItemID.GlassKiln, 1),
				(ItemID.HoneyDispenser, 1),
				(ItemID.IceMachine, 1),
				(ItemID.LivingLoom, 1),
				(ItemID.SkyMill, 1),
				(ItemID.Solidifier, 1));
		}

		public override void GetItemDimensions(out int width, out int height)
		{
			width = 30;
			height = 30;
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.BoneWelder)
				.AddIngredient(ItemID.GlassKiln)
				.AddIngredient(ItemID.HoneyDispenser)
				.AddIngredient(ItemID.IceMachine)
				.AddIngredient(ItemID.LivingLoom)
				.AddIngredient(ItemID.SkyMill)
				.AddIngredient(ItemID.Solidifier)
				.Register();
		}
	}
}
