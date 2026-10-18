import { Button, Modal, Segmented, Switch } from '../../components/ui/ui';
import { useEffectsSettings } from '../../state/EffectsContext';
import styles from './SettingsPanel.module.css';

export function SettingsPanel({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { settings, update, reset } = useEffectsSettings();

  return (
    <Modal open={open} onClose={onClose} title="Configurações" variant="drawer-right">
      <div className={styles.body}>
        <section>
          <h3 className={styles.heading}>Efeitos visuais</h3>
          <p className={styles.note}>Os efeitos são decorativos e podem ser desligados a qualquer momento sem afetar a consulta.</p>
          <div className={styles.stack}>
            <Switch checked={settings.scanlines} onChange={(v) => update({ scanlines: v })} label="Scanlines (efeito CRT)" />
            <Switch checked={settings.glitch} onChange={(v) => update({ glitch: v })} label="Glitch nos títulos" />
            <Switch checked={settings.motion} onChange={(v) => update({ motion: v })} label="Animações e neon piscante" />
            <Switch checked={settings.cityArt} onChange={(v) => update({ cityArt: v })} label="Skyline de Night City no cabeçalho" />
          </div>
        </section>

        <section>
          <h3 className={styles.heading}>Intensidade do neon</h3>
          <div className={styles.rangeRow}>
            <input type="range" min={0} max={1.5} step={0.25} value={settings.glow}
              onChange={(e) => update({ glow: Number(e.target.value) })} aria-label="Intensidade do brilho neon" />
            <span className="mono">{Math.round(settings.glow * 100)}%</span>
          </div>
        </section>

        <section>
          <h3 className={styles.heading}>Densidade dos cards</h3>
          <Segmented ariaLabel="Densidade dos cards" value={settings.density}
            onChange={(v) => update({ density: v })}
            options={[{ value: 'comfortable', label: 'Confortável' }, { value: 'compact', label: 'Compacta' }]} />
        </section>

        <Button onClick={reset}>Restaurar padrões</Button>
      </div>
    </Modal>
  );
}
