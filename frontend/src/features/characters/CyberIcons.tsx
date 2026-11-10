import { Activity, BicepsFlexed, Bot, Brain, Cpu, Ear, Eye, Footprints, Hand, HeartPulse, Shield, Shirt, type LucideIcon } from 'lucide-react';
import type { RegionKey } from './anatomy';

const REGION: Record<RegionKey, LucideIcon> = {
  neural: Brain, 'eye-left': Eye, 'eye-right': Eye, audio: Ear, internal: HeartPulse, external: Shield, borgware: Bot,
  'arm-left': BicepsFlexed, 'arm-right': BicepsFlexed, 'leg-left': Footprints, 'leg-right': Footprints, fashion: Shirt,
};

const TYPE: Record<string, LucideIcon> = {
  Neuralware: Brain, Cyberoptics: Eye, Cyberaudio: Ear, InternalBody: HeartPulse, ExternalBody: Shield, Cyberlimb: BicepsFlexed,
  LimbOption: Hand, Cyberfinger: Hand, Borgware: Bot, Bioexotic: Activity, Fashionware: Shirt,
};

export function RegionIcon({ region, size = 18 }: { region: RegionKey; size?: number }) {
  const Icon = REGION[region] ?? Cpu;
  return <Icon size={size} aria-hidden />;
}

/** Icon of a cyberware type (catalog `cyberType` / report `type`). */
export function CyberTypeIcon({ type, size = 20 }: { type: string | null | undefined; size?: number }) {
  const Icon = (type && TYPE[type]) || Cpu;
  return <Icon size={size} aria-hidden />;
}
