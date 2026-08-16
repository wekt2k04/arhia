import { Bot, User } from "lucide-react";
import ReactMarkdown from "react-markdown";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { cn } from "@/lib/utils";

export type Message = {
  role: "user" | "assistant";
  texte: string;
  sourcee?: boolean;
  sources?: string[];
};

export function ChatMessage({ message, estEnCours }: { message: Message; estEnCours: boolean }) {
  const estUtilisateur = message.role === "user";

  return (
    <div className={cn("flex gap-3", estUtilisateur && "flex-row-reverse")}>
      <Avatar className="mt-0.5 h-7 w-7 shrink-0">
        <AvatarFallback
          className={estUtilisateur ? "bg-primary text-primary-foreground" : "bg-accent text-accent-foreground"}
        >
          {estUtilisateur ? <User className="h-3.5 w-3.5" aria-hidden /> : <Bot className="h-3.5 w-3.5" aria-hidden />}
        </AvatarFallback>
      </Avatar>

      <div
        className={cn(
          "max-w-[80%] rounded-2xl px-4 py-2 text-sm",
          estUtilisateur
            ? "rounded-tr-sm bg-primary text-primary-foreground"
            : "rounded-tl-sm border border-border bg-card text-card-foreground",
        )}
      >
        {message.texte ? (
          <div className="prose prose-sm max-w-none break-words prose-p:my-1 prose-p:leading-relaxed dark:prose-invert">
            <ReactMarkdown>{message.texte}</ReactMarkdown>
          </div>
        ) : (
          estEnCours && (
            <span className="flex items-center gap-1 py-1" aria-hidden>
              <span className="h-1.5 w-1.5 animate-bounce rounded-full bg-current opacity-60 [animation-delay:-0.3s]" />
              <span className="h-1.5 w-1.5 animate-bounce rounded-full bg-current opacity-60 [animation-delay:-0.15s]" />
              <span className="h-1.5 w-1.5 animate-bounce rounded-full bg-current opacity-60" />
            </span>
          )
        )}

        {!estUtilisateur && message.sources && message.sources.length > 0 && (
          <div className="mt-2 flex flex-wrap gap-1 border-t border-border/60 pt-2">
            {message.sources.map((source) => (
              <span
                key={source}
                className="rounded-full bg-muted px-2 py-0.5 text-xs text-muted-foreground"
              >
                {source}
              </span>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
