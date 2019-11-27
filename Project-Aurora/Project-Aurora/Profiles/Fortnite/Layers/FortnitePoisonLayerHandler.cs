using Aurora.EffectsEngine;
using Aurora.EffectsEngine.Animations;
using Aurora.Settings.Layers;
using Aurora.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Aurora.Profiles.Fortnite.Layers {

    public class FortnitePoisonLayerHandler : LayerHandler<LayerHandlerProperties> {
        public FortnitePoisonLayerHandler() {
            _ID = "FortnitePoisonLayer";
        }

        protected override UserControl CreateControl() {
            return new Control_FortnitePoisonLayer();
        }

        public override EffectLayer Render(IGameState gamestate) {
            EffectLayer layer = new EffectLayer("Forthite Poison Layer");

            // Render nothing if invalid gamestate or player isn't on fire
            if (!(gamestate is GameState_Fortnite) || (gamestate as GameState_Fortnite).Game.Status != "poison")
                return layer;

            // Set the background to Purple
            layer.Fill(Color.Purple);

            return layer;
        }
    }
}
