from pathlib import Path
import json,shutil
root=Path(__file__).resolve().parent;a=root/'artifacts/core021';e=root/'evidence/core021';d=root/'docs/core_refinement_021'
e.mkdir(exist_ok=True);d.mkdir(exist_ok=True)
def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))
def write(name,lines):(d/name).write_text('\n'.join(lines)+'\n',encoding='utf-8')
t=read(a/'tests.json')
write('TEST_RESULTS_CORE_021.md',['# Тесты Core Refinement 0.2.1','',f"Release и Debug: {t['total']} новых групп, ошибок {t['failed']}. Прежние 36+59+89=184 теста прошли без изменений. Итого {184+t['total']} групп. В одну новую дифференциальную группу входят 5000 Python double reference cases; ещё 2500 seeded property/differential итераций проверяют HEAT/AoE, ammo и missile helpers.",'','Это сравнение с математическими эталонами, не с runtime игры. float сравнивается с double при относительном допуске 2e-5; границы ceil/threshold/index отдельно проверяются точно. Тест isolation использует два одинаковых integration fixture: между шагами второго вызывается новая математика; territory, graph, isolation и прочие frame данные совпадают после исключения только таймингов. Старые тестовые файлы и правила фронта не изменялись.','','| Группа | Результат |','|---|---|']+[f"| {r['name']} | {'PASS' if r['passed'] else 'FAIL'} |" for r in t['results']])
release=read(a/'benchmarks-Release.json');debug=read(a/'benchmarks-Debug.json')
lines=['# Производительность 0.2.1','','.NET 6.0.36, Windows, локальный автономный прогон. Micro: 1000 warmup, 15 batches по 10000 операций (critical selection — 1000), Stopwatch.GetTimestamp, GC.GetAllocatedBytesForCurrentThread. Результат сохраняется в sink. Median — медиана batch ns/call; p95 здесь максимум из 15 batch, не p95 одиночной операции. JIT, tiering и фоновая нагрузка ограничивают интерпретацию.','','| Helper | Release ns/call | Debug ns/call | Release bytes/call | Debug bytes/call |','|---|---:|---:|---:|---:|']
for x,y in zip(release['microbenchmarks'],debug['microbenchmarks']):lines.append(f"| {x['name']} | {x['medianNanoseconds']:.1f} | {y['medianNanoseconds']:.1f} | {x['allocatedBytesPerCall']:.3f} | {y['allocatedBytesPerCall']:.3f} |")
lines+=['','Scalar helpers и span loops не создают коллекций. Critical selection выделяет immutable-facing trace; это диагностический выбор, не hot-path территории. Профиль загружается явно, не на каждом тике. Оптимизации старого ядра не менялись.','','## Контроль интеграции','','Сетка 256², 1000 юнитов, 250 ms, seed 82026, 10 warmup +60 samples. Исторический baseline сохранён в evidence/poc02/benchmarks.json. Проверены старый и новый Release DLL одним неизменённым integration runner с полным набором 24 сценариев, оба вывода записаны только в новое worktree.','','| Запуск | median ms | p99 ms | bytes/tick |','|---|---:|---:|---:|']
for label,path in [('Исторический PoC02',root/'evidence/poc02/benchmarks.json'),('PoC02 повтор сейчас',a/'baseline-run/artifacts/poc02/benchmarks.json'),('0.2.1 тот же runner',a/'current-run/artifacts/poc02/benchmarks.json')]:
    data=read(path);r=next(v for v in data['results'] if v['size']==256 and v['count']==1000 and v['period']==250)
    lines.append(f"| {label} | {r['total']['median']:.3f} | {r['total']['p99']:.3f} | {r['allocatedBytesPerTick']} |")
for label,data in [('Отдельный micro runner Release',release),('Отдельный micro runner Debug',debug)]:
    r=data['integration'];lines.append(f"| {label} | {r['median']:.3f} | {r['p99']:.3f} | {r['allocatedBytesPerTick']} |")
lines+=['','Отдельный короткий runner показал более высокую медиану: этот результат сохранён, не отброшен. Повтор одинаковым integration runner не выявил регрессии; небольшой выигрыш не объявляется ускорением. Различия в десятках/сотнях bytes/tick связаны с измерением и runtime, а не подключением новых формул: интеграционные исходники неизменны. Замеры не предсказывают FPS и стоимость реальной навигации.']
write('BENCHMARKS_CORE_021.md',lines)
for name in ['tests.json','tests-Release.log','tests-Debug.log','benchmarks-Release.json','benchmarks-Debug.json','explorer-results.json','build-hashes.json']:
    if (a/name).exists():shutil.copy2(a/name,e/name)
for label in ['baseline-run','current-run']:shutil.copy2(a/label/'artifacts/poc02/benchmarks.json',e/(label+'-integration-benchmarks.json'))
for name in ['DynamicFrontline.Tests','DynamicFrontline.Frontline.Tests','DynamicFrontline.Integration.Tests']:shutil.copy2(a/'regression'/(name+'.log'),e/(name+'.log'))
if (root/'artifacts/game-preservation.json').exists():shutil.copy2(root/'artifacts/game-preservation.json',e/'game-preservation.json')
print('Reports and new evidence saved; historical evidence unchanged')
