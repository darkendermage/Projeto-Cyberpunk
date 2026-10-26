import { useQuery } from '@tanstack/react-query';
import { api } from '../../api/client';
import { Button, Modal, Segmented, Switch } from '../../components/ui/ui';
import { useEffectsSettings } from '../../state/EffectsContext';
import styles from './SettingsPanel.module.css';
import { lang, setLang, t, type Lang } from '../../i18n';

export function SettingsPanel({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { settings, update, reset } = useEffectsSettings();
  const health = useQuery({ queryKey: ['health'], queryFn: ({ signal }) => api.health(signal), enabled: open, staleTime: Infinity });

  return (
    <Modal open={open} onClose={onClose} title={t('Configurações', 'Settings')} variant="drawer-right">
      <div className={styles.body}>
        <section>
          <h3 className={styles.heading}>Language / Idioma</h3>
          <Segmented<Lang> ariaLabel="Language / Idioma" value={lang} onChange={setLang}
            options={[{ value: 'en', label: 'English' }, { value: 'pt', label: 'Português (BR)' }]} />
          <p className={styles.note}>{t('A página recarrega para aplicar a língua. Termos do livro (Death Save, nomes de itens) ficam em inglês.', 'The page reloads to apply the language. Book terms (Death Save, item names) stay in English.')}</p>
        </section>

        <section>
          <h3 className={styles.heading}>{t('Efeitos visuais', 'Visual effects')}</h3>
          <p className={styles.note}>{t('Os efeitos são decorativos e podem ser desligados a qualquer momento sem afetar a consulta.', 'The effects are decorative and can be turned off at any time without affecting lookups.')}</p>
          <div className={styles.stack}>
            <Switch checked={settings.scanlines} onChange={(v) => update({ scanlines: v })} label={t('Scanlines (efeito CRT)', 'Scanlines (CRT effect)')} />
            <Switch checked={settings.glitch} onChange={(v) => update({ glitch: v })} label={t('Glitch nos títulos', 'Glitch on titles')} />
            <Switch checked={settings.motion} onChange={(v) => update({ motion: v })} label={t('Animações e neon piscante', 'Animations and flickering neon')} />
            <Switch checked={settings.cityArt} onChange={(v) => update({ cityArt: v })} label={t('Skyline de Night City no cabeçalho', 'Night City skyline in the header')} />
          </div>
        </section>

        <section>
          <h3 className={styles.heading}>{t('Intensidade do neon', 'Neon intensity')}</h3>
          <div className={styles.rangeRow}>
            <input type="range" min={0} max={1.5} step={0.25} value={settings.glow}
              onChange={(e) => update({ glow: Number(e.target.value) })} aria-label={t('Intensidade do brilho neon', 'Neon glow intensity')} />
            <span className="mono">{Math.round(settings.glow * 100)}%</span>
          </div>
        </section>

        <section>
          <h3 className={styles.heading}>{t('Densidade dos cards', 'Card density')}</h3>
          <Segmented ariaLabel={t('Densidade dos cards', 'Card density')} value={settings.density}
            onChange={(v) => update({ density: v })}
            options={[{ value: 'comfortable', label: t('Confortável', 'Comfortable') }, { value: 'compact', label: t('Compacta', 'Compact') }]} />
        </section>

        <Button onClick={reset}>{t('Restaurar padrões', 'Restore defaults')}</Button>

        {health.data && <p className={styles.version}>{t('Versão ', 'Version ')}<span className="mono">{health.data.version}</span></p>}
      </div>
    </Modal>
  );
}
