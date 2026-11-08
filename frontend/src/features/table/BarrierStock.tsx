import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { BrickWall, Plus } from 'lucide-react';
import { tableApi, tableKeys } from '../../api/table';
import type { SceneView, TokenView } from '../../api/tableTypes';
import { Button, Field, IconButton, Segmented, Select, Spinner } from '../../components/ui/ui';
import { t, tv } from '../../i18n';
import { barrierState } from './board/barriers';
import { NumField } from './NumField';
import type { Act } from './GmPanels';
import styles from './Table.module.css';

type Mode = 'example' | 'material';

// Book barriers (CP:R p.182-183), an example or material + thickness. They go on the scenery layer and become
// the fight's cover when there is one.
export function BarrierStock({ scene, act, at }: { scene: SceneView; act: Act; at: () => { x: number; y: number } }) {
  const rules = useQuery({ queryKey: tableKeys.rules, queryFn: tableApi.rules, staleTime: Infinity });
  const [mode, setMode] = useState<Mode>('example');
  const [example, setExample] = useState(0);
  const [material, setMaterial] = useState('concrete');
  const [thick, setThick] = useState(true);
  const [label, setLabel] = useState('');
  const [size, setSize] = useState({ width: 2, height: 1 });
  if (rules.isLoading) return <Spinner />;
  const data = rules.data;
  if (!data) return null;

  const picked = data.coverExamples[example];
  const mat = data.coverMaterials.find((m) => m.id === material);
  const choice = mode === 'example' && picked
    ? { label: tv(picked.label), material: picked.material, thick: picked.thick, hp: picked.hp as number | null }
    : { label: mat ? tv(mat.label) : material, material, thick, hp: null };
  const hp = choice.hp ?? (mat ? (thick ? mat.thick : mat.thin) : 0);

  return (
    <form className={styles.section} onSubmit={(e) => {
      e.preventDefault();
      const c = at();
      void act(() => tableApi.addBarrier(scene.id, {
        label: (label.trim() || choice.label).slice(0, 40), material: choice.material, thick: choice.thick, hp: choice.hp,
        x: c.x, y: c.y, width: size.width, height: size.height,
      }), t('Barreira no mapa.', 'Barrier on the map.'));
    }}>
      <span className={styles.label}>{t('Barreiras', 'Barriers')}</span>
      <Segmented<Mode> size="sm" ariaLabel={t('Origem da barreira', 'Barrier source')} value={mode} onChange={setMode}
        options={[{ value: 'example', label: t('Exemplo do livro', 'Book example') }, { value: 'material', label: t('Material', 'Material') }]} />
      {mode === 'example' ? (
        <>
          <Select aria-label={t('Exemplo de cobertura', 'Cover example')} value={example} onChange={(e) => setExample(Number(e.target.value))}>
            {data.coverExamples.map((c, i) => <option key={c.label} value={i}>{tv(c.label)} · {c.hp} HP</option>)}
          </Select>
          <span className={styles.source}>{data.coverExamplesSource}</span>
        </>
      ) : (
        <>
          <div className={styles.row}>
            <Select aria-label={t('Material', 'Material')} value={material} onChange={(e) => setMaterial(e.target.value)}>
              {data.coverMaterials.map((m) => <option key={m.id} value={m.id}>{tv(m.label)}</option>)}
            </Select>
            <Segmented<'thick' | 'thin'> size="sm" ariaLabel={t('Espessura', 'Thickness')} value={thick ? 'thick' : 'thin'} onChange={(v) => setThick(v === 'thick')}
              options={[{ value: 'thick', label: t('Grossa', 'Thick') }, { value: 'thin', label: t('Fina', 'Thin') }]} />
          </div>
          <span className={styles.source}>{data.coverMaterialsSource}</span>
        </>
      )}
      <div className={styles.row}>
        <input className={styles.grow} aria-label={t('Nome da barreira', 'Barrier name')} value={label} maxLength={40} placeholder={choice.label} onChange={(e) => setLabel(e.target.value)} />
      </div>
      <div className={styles.row}>
        <Field label={t('Largura', 'Width')}><NumField label={t('Largura da barreira em células', 'Barrier width in cells')} value={size.width} min={0.25} max={20} step={0.5} onChange={(width) => setSize({ ...size, width })} /></Field>
        <Field label={t('Altura', 'Height')}><NumField label={t('Altura da barreira em células', 'Barrier height in cells')} value={size.height} min={0.25} max={20} step={0.5} onChange={(height) => setSize({ ...size, height })} /></Field>
        <span className={styles.stat}>{hp} HP</span>
      </div>
      {scene.encounterId != null && <span className={styles.muted}>{t('A cena tem combate: a barreira também entra como cobertura dele.', 'The scene has a fight: the barrier also becomes its cover.')}</span>}
      <Button type="submit" icon={<Plus size={15} />} disabled={hp <= 0}>{hp <= 0 ? t('Não para bala', "Doesn't stop bullets") : t('Pôr barreira no mapa', 'Put barrier on the map')}</Button>
    </form>
  );
}

/** A barrier's HP for the GM: damage it (an attack through it, CP:R p.183), or repair it while it is no fight's cover. */
export function BarrierControls({ scene, token, act }: { scene: SceneView; token: TokenView; act: Act }) {
  const [amount, setAmount] = useState(5);
  const b = token.barrier!;
  const state = barrierState(b);
  const hit = (n: number) => act(() => tableApi.damageBarrier(scene.id, token.id, n));
  return (
    <div className={styles.section}>
      <span className={styles.rowBetween}>
        <span className={styles.label}><BrickWall size={13} /> {t('Barreira', 'Barrier')}</span>
        <span className={styles.stat}>{state.text}</span>
      </span>
      <div className={styles.meter}><div className={styles.meterFill} style={{ width: `${state.ratio * 100}%` }} /></div>
      <div className={styles.row}>
        <NumField label={t('Dano na barreira', 'Damage to the barrier')} value={amount} min={1} max={999} onChange={(n) => setAmount(Math.max(1, Math.round(n)))} />
        <Button onClick={() => hit(amount)} disabled={state.destroyed}>{t('Dano', 'Damage')}</Button>
        {!b.coverId && <IconButton label={t(`Reparar ${amount} HP`, `Repair ${amount} HP`)} disabled={b.hp >= b.maxHp} onClick={() => hit(-amount)}><Plus size={14} /></IconButton>}
      </div>
      <span className={styles.muted}>
        {b.coverId
          ? t(`Cobertura ${b.coverId} do combate: o dano feito na mesa de combate também aparece aqui.`, `The fight's cover ${b.coverId}: damage done on the combat table shows here too.`)
          : t('A 0 HP a barreira não protege mais (CP:R p.183).', 'At 0 HP the barrier no longer protects (CP:R p.183).')}
      </span>
    </div>
  );
}
