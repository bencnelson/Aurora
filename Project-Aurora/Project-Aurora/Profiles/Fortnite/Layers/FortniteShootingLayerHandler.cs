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

    public class FortniteShootingLayerHandler : LayerHandler<LayerHandlerProperties> {
        public const string STATUS = "shooting";

        private static Color[] colors = new Color[] {
            Color.FromArgb(250, 53, 15),
            Color.FromArgb(250, 113, 15),
            Color.FromArgb(250, 172, 15),
            Color.FromArgb(250, 211, 15),
        };

        private Random rnd = new Random();

        private uint nframeDelay = ANIMATION_DUARATION + 1;
        const uint ANIMATION_DUARATION = 7; //frames

        public FortniteShootingLayerHandler() {
            _ID = "FortniteShootingLayer";
        }

        protected override UserControl CreateControl() {
            return new Control_FortniteShootingLayer();
        }

        public override EffectLayer Render(IGameState gamestate) {
            EffectLayer layer = new EffectLayer($"Fortnite {STATUS} Layer");

            if (!(gamestate is GameState_Fortnite))
                return layer;

            if ((gamestate as GameState_Fortnite).Game.Status == STATUS)
            {
                nframeDelay = 0;
            }
            else if (nframeDelay > ANIMATION_DUARATION)
            {
                return layer;
            }

            layer.Fill(Color.Black);

            int w = layer.GetBitmap().Width;
            int h = layer.GetBitmap().Height;

            for (int i = 0; i < 10; ++i)
            {
                int rNum = rnd.Next();
                layer.Set((Devices.DeviceKeys)(rNum % 216), colors[rNum % colors.Length]);
            }

            nframeDelay++;

            return layer;
        }
    }
}
