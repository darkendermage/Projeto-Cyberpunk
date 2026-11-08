import { useEffect, useState, type ReactNode } from 'react';
import { useNavigate } from 'react-router';
import { useMutation, useQuery } from '@tanstack/react-query';
import { lobbyApi, playerApi, tableKeys } from '../../api/table';
import type { LobbySeat } from '../../api/tableTypes';
import { Button, Spinner } from '../../components/ui/ui';
import { useToast } from '../../state/ToastContext';
import { t } from '../../i18n';
import { claimErrorMessage, forgetSeat, isRefusedKey, rememberSeat, savedSeat } from './lobby';
import { LangSwitch } from './LangSwitch';
import { initials } from './tokens';
import styles from './Table.module.css';

// /mesa/j/ - the tunnel address lands here. A device that already has a seat goes back to it; otherwise the
// player picks a free seat while the GM keeps the lobby open.
export function LobbyPage() {
  const navigate = useNavigate();
  const toast = useToast();
  const [checking, setChecking] = useState(() => savedSeat() !== null);

  useEffect(() => {
    const saved = savedSeat();
    if (!saved) return;
    let gone = false;
    // A refused key is dropped and the entrance shows; on any other failure the seat's own page retries.
    playerApi(saved).me().then(
      () => { if (!gone) navigate(`/mesa/j/${saved}`, { replace: true }); },
      (error) => {
        if (!isRefusedKey(error)) { if (!gone) navigate(`/mesa/j/${saved}`, { replace: true }); return; }
        forgetSeat();
        if (!gone) setChecking(false);
      },
    );
    return () => { gone = true; };
  }, [navigate]);

  const lobby = useQuery({ queryKey: tableKeys.lobby, queryFn: lobbyApi.list, enabled: !checking, refetchInterval: 5000, retry: false });
  const claim = useMutation({
    mutationFn: lobbyApi.claim,
    onSuccess: (result) => { rememberSeat(result.token); navigate(`/mesa/j/${result.token}`, { replace: true }); },
    onError: (error) => { toast.notify(claimErrorMessage(error).text, 'error'); void lobby.refetch(); },
  });

  const card = (title: string, body: ReactNode) => (
    <div className={styles.gate}>
      <div className={styles.gateCard}>
        <div className={styles.rowBetween}>
          <span className={styles.brand}>Night City // {t('Mesa', 'Table')}</span>
          <LangSwitch />
        </div>
        <h1>{title}</h1>
        {body}
      </div>
    </div>
  );

  if (checking || lobby.isLoading) return card(t('Entrando…', 'Entering…'), <Spinner />);
  if (lobby.error || !lobby.data) {
    return card(t('Sem sinal', 'No signal'), (
      <>
        <p className={styles.muted}>{t('Não deu para falar com a mesa do mestre. O túnel pode ter fechado ou trocado de endereço.', 'Couldn\'t reach the GM\'s table. The tunnel may have closed or changed address.')}</p>
        <Button onClick={() => lobby.refetch()}>{t('Tentar de novo', 'Try again')}</Button>
      </>
    ));
  }
  if (!lobby.data.open) {
    return card(t('A entrada está fechada', 'The table is closed'), (
      <p className={styles.muted}>{t('Peça ao mestre para abrir a entrada. Esta página tenta de novo sozinha.', 'Ask the GM to open it. This page tries again by itself.')}</p>
    ));
  }
  return card(t('Escolha seu personagem', 'Pick your character'), lobby.data.seats.length === 0
    ? <p className={styles.muted}>{t('Todos os lugares já foram escolhidos. Se o seu ficou preso em outro aparelho, peça ao mestre para liberá-lo.', 'Every seat is taken. If yours is stuck on another device, ask the GM to release it.')}</p>
    : (
      <div className={styles.lobbyGrid}>
        {lobby.data.seats.map((s) => <SeatCard key={s.id} seat={s} disabled={claim.isPending} onPick={() => claim.mutate(s.id)} />)}
      </div>
    ));
}

function SeatCard({ seat, disabled, onPick }: { seat: LobbySeat; disabled: boolean; onPick: () => void }) {
  const name = seat.characterName ?? seat.label;
  return (
    <button type="button" className={styles.lobbySeat} disabled={disabled} onClick={onPick} style={{ borderColor: seat.color }}>
      {seat.hasPortrait
        ? <img src={lobbyApi.portraitUrl(seat.id)} alt="" />
        : <span className={styles.lobbyInitials} style={{ background: seat.color }}>{initials(name)}</span>}
      <strong>{name}</strong>
      {seat.characterName && <span className={styles.muted}>{seat.label}</span>}
    </button>
  );
}
