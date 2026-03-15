import Link from "next/link";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

export default function Page() {
  return (
    <div className="container mx-auto flex min-h-screen items-center justify-center p-6">
      <Card className="w-full max-w-lg">
        <CardHeader>
          <CardTitle>403 — Access denied</CardTitle>
          <CardDescription>
            Your current role does not have permission to access this dashboard route.
          </CardDescription>
        </CardHeader>
        <CardContent className="flex gap-3">
          <Button asChild>
            <Link href="/dashboard/login/v2">Admin login</Link>
          </Button>
          <Button variant="outline" asChild>
            <Link href="/dashboard/login/v1">Customer login</Link>
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}
