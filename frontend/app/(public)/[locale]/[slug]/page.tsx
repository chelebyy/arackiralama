import ManagedPageContent from "@/components/public/ManagedPageContent";

type ManagedSlugPageProps = {
  params: Promise<{
    slug: string;
  }>;
};

export default async function ManagedSlugPage({ params }: ManagedSlugPageProps) {
  const { slug } = await params;

  return <ManagedPageContent slug={slug} />;
}
