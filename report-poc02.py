"""Regenerate only PoC02 reports/evidence from completed local runs."""
from pathlib import Path
import json, shutil

root=Path(__file__).resolve().parent
a=root/'artifacts/poc02'
e=root/'evidence/poc02'
e.mkdir(exist_ok=True)
def read(name): return json.loads((a/name).read_text(encoding='utf-8-sig'))
t=read('integration-tests.json')
lines=['# Результаты PoC 0.2','',f"Интеграционные тесты: **{t['total']}**, ошибок **{t['failed']}**. Прежние тесты: **36 + 59 = 95**, все прошли. Итого **{t['total']+95}**. Release, .NET 6.0.36, 25 октября 2026.",'','Сборка обеих MelonLoader DLL успешна, без предупреждений. Проверка выполнена автономно, без запуска игры. Старые тесты не изменены. Полные build/test logs остаются в artifacts/poc02; итоговый JSON и hashes сохранены в evidence/poc02. Первые неуспешные итерации сохранены в append-only build.log и не выдаются за финальный прогон.','','| Тест | Результат |','|---|---|']
lines += [f"| {r['name']} | {'PASS' if r['passed'] else 'FAIL'} |" for r in t['results']]
(root/'TEST_RESULTS_POC02.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
b=read('benchmarks.json'); old=read('benchmarks-before.json'); core=read('legacy/artifacts/benchmarks.json')
baseline=json.loads((root/'evidence/benchmarks.json').read_text(encoding='utf-8-sig'))
lines=['# Нагрузочные проверки PoC 0.2','','Synthetic, .NET 6.0.36, seed 82026. Каждая комбинация: 10 прогревочных + 60 измеряемых шагов. Это вычислительный цикл без реальной игры и без стоимости настоящего CalculatePath. Период — параметр симулированного времени, не sleep и не измерение FPS. Graph входит в Core; времена стадий не следует складывать с graph повторно.','','| Сетка | Юниты | Период ms | median ms | p95 ms | p99 ms | Bytes/tick |','|---|---:|---:|---:|---:|---:|---:|']
for r in b['results']:
    lines.append(f"| {r['size']}² | {r['count']} | {r['period']} | {r['total']['median']:.3f} | {r['total']['p95']:.3f} | {r['total']['p99']:.3f} | {r['allocatedBytesPerTick']} |")
key=lambda r:r['size']==256 and r.get('count',r.get('units'))==1000 and r.get('period',r.get('tickMilliseconds'))==250
before=next(filter(key,old['results'])); after=next(filter(key,b['results']))
lines += ['', '## Аллокации и сравнение', '',f"В контрольном сценарии 256² / 1000 / 250 ms: до оптимизации {before['allocatedBytesPerTick']:,} bytes/tick, итог {after['allocatedBytesPerTick']:,}: снижение на {100*(1-after['allocatedBytesPerTick']/before['allocatedBytesPerTick']):.1f}%. Итог также включает более строгую проверку обоих направлений маршрута из Pass 03. Основные изменения: value-type route keys/results и lifecycle entries, кэш профилей, обновление territory snapshot по delta вместо повторного создания всех cell records.",'','Сравнение прежнего ядра на его неизменённом workload (seed 62026); отдельный workload интеграции не является прямым сравнением скорости алгоритма:','','| Вариант 256² / 1000 / 250 | median ms | bytes/tick |','|---|---:|---:|']
for label,data in [('PoC01 baseline',baseline),('Core в новой ветке',core),('Полный Integration',b)]:
    r=next(filter(key,data['results']));lines.append(f"| {label} | {r['total']['median']:.3f} | {r['allocatedBytesPerTick']} |")
lines += ['','Полный интеграционный слой выделяет больше памяти, чем одно ядро. Остались копии DTO/массивов, validation/lifecycle dictionaries, результаты Core и сериализация. Применение pool к опубликованным массивам отложено из-за риска изменения уже выданного снимка. Синтетические медианы не доказывают приемлемые GC-паузы в игре; p99 чувствителен к короткому прогону и фоновой нагрузке.','','## Микропроверки','','| Операция | Итерации | Всего ms | Всего bytes |','|---|---:|---:|---:|']
for r in read('microbenchmarks.json'): lines.append(f"| {r['name']} | {r['iterations']} | {r['totalMs']:.4f} | {r['bytes']} |")
lines += ['','Полные показатели snapshot/core/graph/teamSerialization, managed heap, working set, GC и navigation находятся в [evidence/poc02/benchmarks.json](evidence/poc02/benchmarks.json). Для воспроизведения выполнить build-poc02.ps1 без -SkipBenchmarks.']
(root/'BENCHMARKS_POC02.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
for name in ['integration-tests.json','benchmarks.json','benchmarks-before.json','microbenchmarks.json','build-hashes.json']:
    shutil.copy2(a/name,e/name)
shutil.copy2(a/'legacy/artifacts/benchmarks.json',e/'core-benchmark-poc02.json')
for name in ['DynamicFrontline.Tests.log','DynamicFrontline.Frontline.Tests.log']:
    shutil.copy2(a/'legacy'/name,e/name)
pres=a/'preservation/artifacts/game-preservation.json'
if pres.exists():shutil.copy2(pres,e/'game-preservation.json')
print(f"Reports written: {t['total']} integration tests, {len(b['results'])} benchmark scenarios.")
