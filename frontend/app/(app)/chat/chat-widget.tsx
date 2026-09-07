"use client";

import { useEffect, useRef, useState, type FormEvent } from "react";
import { Send } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Card } from "@/components/ui/card";
import { ChatMessage, type Message } from "@/components/chat-message";

export function ChatWidget() {
  const [messages, setMessages] = useState<Message[]>([]);
  const [question, setQuestion] = useState("");
  const [enCours, setEnCours] = useState(false);
  const eventSourceRef = useRef<EventSource | null>(null);
  const finDesMessagesRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    finDesMessagesRef.current?.scrollIntoView({ behavior: "smooth", block: "end" });
  }, [messages]);

  function envoyer(evenement: FormEvent) {
    evenement.preventDefault();
    const texteQuestion = question.trim();
    if (!texteQuestion || enCours) return;

    eventSourceRef.current?.close();

    setMessages((precedents) => [
      ...precedents,
      { role: "user", text: texteQuestion },
      { role: "assistant", text: "" },
    ]);
    setQuestion("");
    setEnCours(true);

    const url = `/api/chat/ask?question=${encodeURIComponent(texteQuestion)}`;
    const source = new EventSource(url);
    eventSourceRef.current = source;

    function mettreAJourDernierMessage(maj: (message: Message) => Message) {
      setMessages((precedents) => {
        const copie = [...precedents];
        const dernierIndex = copie.length - 1;
        copie[dernierIndex] = maj(copie[dernierIndex]);
        return copie;
      });
    }

    source.addEventListener("fragment", (evt) => {
      const donnees = JSON.parse((evt as MessageEvent).data) as { text: string };
      mettreAJourDernierMessage((m) => ({ ...m, text: m.text + donnees.text }));
    });

    source.addEventListener("done", (evt) => {
      const donnees = JSON.parse((evt as MessageEvent).data) as { sourced: boolean; sources: string[] };
      mettreAJourDernierMessage((m) => ({ ...m, sourced: donnees.sourced, sources: donnees.sources }));
      source.close();
      setEnCours(false);
    });

    source.onerror = () => {
      source.close();
      setEnCours(false);
    };
  }

  return (
    <Card className="flex h-full w-full max-w-3xl flex-col overflow-hidden py-0">
      <ScrollArea className="min-h-0 flex-1">
        <div className="space-y-4 p-4">
          {messages.length === 0 && (
            <p className="py-8 text-center text-sm text-muted-foreground">
              Posez une question sur les politiques internes ou sur l&apos;avancement de votre
              dossier.
            </p>
          )}
          {messages.map((message, index) => (
            <ChatMessage
              key={index}
              message={message}
              estEnCours={enCours && index === messages.length - 1}
            />
          ))}
          <div ref={finDesMessagesRef} />
        </div>
      </ScrollArea>

      <p role="status" aria-live="polite" className="sr-only">
        {enCours ? "L'assistant est en train de répondre." : ""}
      </p>

      <form onSubmit={envoyer} className="flex gap-2 border-t border-border p-4">
        <label htmlFor="question-chat" className="sr-only">
          Votre question
        </label>
        <Input
          id="question-chat"
          type="text"
          value={question}
          onChange={(e) => setQuestion(e.target.value)}
          disabled={enCours}
          placeholder="Posez votre question..."
          className="flex-1"
        />
        <Button type="submit" disabled={enCours || !question.trim()} size="icon">
          <Send aria-hidden />
          <span className="sr-only">Envoyer</span>
        </Button>
      </form>
    </Card>
  );
}
