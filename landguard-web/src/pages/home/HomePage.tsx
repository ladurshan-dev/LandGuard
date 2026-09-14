import { PublicLayout } from '../../layouts/PublicLayout';
import { HomeHero } from '../../components/home/HomeHero';
import { HomeSearch } from '../../components/home/HomeSearch';
import { HomeFeatures } from '../../components/home/HomeFeatures';
import { HomeFeaturedProperties } from '../../components/home/HomeFeaturedProperties';
import { HomeHowItWorks } from '../../components/home/HomeHowItWorks';
import { HomeSafety } from '../../components/home/HomeSafety';
import { HomeCta } from '../../components/home/HomeCta';

/**
 * The public LandGuard homepage - renders at "/" for every visitor,
 * signed in or not (see AppRoutes.tsx's own doc comment for why the old
 * auth-based redirect was removed). Composed from small, single-purpose
 * sections rather than one large file, each already documented with why
 * its copy/behavior is what it is (no fabricated statistics, no
 * automatic-approval framing, no property-type search field, etc.).
 */
export default function HomePage() {
  return (
    <PublicLayout>
      <HomeHero />
      <HomeSearch />
      <HomeFeatures />
      <HomeFeaturedProperties />
      <HomeHowItWorks />
      <HomeSafety />
      <HomeCta />
    </PublicLayout>
  );
}
