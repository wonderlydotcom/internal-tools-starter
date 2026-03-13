import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

const App = () => {
  return (
    <main className="min-h-screen bg-background text-foreground">
      <section className="mx-auto flex min-h-screen w-full max-w-3xl items-center px-6 py-20">
        <Card className="w-full">
          <CardHeader>
            <CardTitle>FsharpStarter</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <p>Hexagonal F# starter template with EF Core + SQLite defaults.</p>
            <p>
              Optional capabilities like IAP auth, OpenFGA, and shared-platform
              deployment are documented in <code>.agents/skills</code>.
            </p>
          </CardContent>
        </Card>
      </section>
    </main>
  );
};

export default App;
