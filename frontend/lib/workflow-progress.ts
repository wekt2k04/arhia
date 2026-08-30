export function calculerProgression(total: number, done: number, failed: number) {
  const traites = done + failed;
  return { traites, total, pourcentage: total === 0 ? 0 : Math.round((traites / total) * 100) };
}
