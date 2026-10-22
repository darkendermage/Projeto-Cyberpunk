import { useState } from 'react';
import { Download, RefreshCw, TriangleAlert, X } from 'lucide-react';
import { api } from '../../api/client';
import { useUpdateInfo } from '../../api/hooks';
import { readStorage, writeStorage } from '../../lib/hooks';
import { shouldOfferUpdate, waitForNewVersion } from '../../lib/updates';
import { Button } from '../ui/ui';
import styles from './UpdateBanner.module.css';

const DISMISSED_KEY = 'ncdb.dismissedUpdate';

type Phase =
  | { kind: 'offer' }
  | { kind: 'installing' }
  | { kind: 'restarting' }
  | { kind: 'failed'; message: string }
  | { kind: 'timeout' };

/** Offers the latest GitHub release when running as the desktop executable (Desktop/AppUpdater.cs). */
export function UpdateBanner() {
  const { data } = useUpdateInfo();
  const [dismissed, setDismissed] = useState(() => readStorage(DISMISSED_KEY, { version: null as string | null }).version);
  const [phase, setPhase] = useState<Phase>({ kind: 'offer' });
  const [notesOpen, setNotesOpen] = useState(false);

  if (!shouldOfferUpdate(data, dismissed)) return null;
  const info = data;

  const dismiss = () => {
    setDismissed(info.latest);
    writeStorage(DISMISSED_KEY, { version: info.latest });
  };

  const install = async () => {
    setPhase({ kind: 'installing' });
    try {
      await api.applyUpdate(info.latest);
    } catch (error) {
      setPhase({ kind: 'failed', message: (error as Error).message });
      return;
    }
    setPhase({ kind: 'restarting' });
    if (await waitForNewVersion(info.current, () => api.health())) window.location.reload();
    else setPhase({ kind: 'timeout' });
  };

  const busy = phase.kind === 'installing' || phase.kind === 'restarting';

  return (
    <div className={styles.banner} role="status">
      <div className={styles.row}>
        {phase.kind === 'failed' || phase.kind === 'timeout'
          ? <TriangleAlert size={18} className={styles.icon} />
          : <Download size={18} className={styles.icon} />}

        <p className={styles.text}>
          {phase.kind === 'offer' && <>Versão <strong>{info.latest}</strong> disponível (você está na {info.current}).</>}
          {phase.kind === 'installing' && <>Baixando e instalando a versão <strong>{info.latest}</strong>…</>}
          {phase.kind === 'restarting' && <>Reiniciando o programa. A página recarrega sozinha.</>}
          {phase.kind === 'failed' && <>Não foi possível atualizar: {phase.message}</>}
          {phase.kind === 'timeout' && <>O programa não voltou sozinho. Abra o NightCityItemDB.exe de novo.</>}
        </p>

        <div className={styles.actions}>
          {info.notes && !busy && (
            <Button variant="ghost" onClick={() => setNotesOpen((v) => !v)} aria-expanded={notesOpen}>
              {notesOpen ? 'Ocultar novidades' : 'Novidades'}
            </Button>
          )}
          {(phase.kind === 'offer' || phase.kind === 'failed') && (
            <Button variant="primary" icon={<RefreshCw size={15} />} onClick={install}>
              {phase.kind === 'failed' ? 'Tentar de novo' : 'Atualizar agora'}
            </Button>
          )}
          {phase.kind === 'failed' && info.releaseUrl && (
            <a className={styles.link} href={info.releaseUrl} target="_blank" rel="noreferrer">Baixar manualmente</a>
          )}
          {!busy && (
            <button type="button" className={styles.close} onClick={dismiss} aria-label="Lembrar depois" title="Lembrar na próxima versão">
              <X size={16} />
            </button>
          )}
        </div>
      </div>

      {notesOpen && info.notes && <pre className={styles.notes}>{info.notes}</pre>}
    </div>
  );
}
