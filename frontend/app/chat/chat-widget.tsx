"use client";

import { useRef, useState, type FormEvent } from "react";
import ReactMarkdown from "react-markdown";

type Message = {
  role: "user" | "assistant";
  texte: string;
  sourcee?: boolean;
  sources?: string[];
};

export function ChatWidget() {
  const [messages, setMessages] = useState<Message[]>([]);
  const [question, setQuestion] = useState("");
  const [enCours, setEnCours] = useState(false);
  const eventSourceRef = useRef<EventSource | null>(null);

  function envoyer(evenement: FormEvent) {
    evenement.preventDefault();
    const texteQuestion = question.trim();
    if (!texteQuestion || enCours) return;

    eventSourceRef.current?.close();

    setMessages((precedents) => [
      ...precedents,
      { role: "user", texte: texteQuestion },
      { role: "assistant", texte: "" },
    ]);
    setQuestion("");
    setEnCours(true);

    const url = `/api/chat/demander?question=${encodeURIComponent(texteQuestion)}`;
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
      const donnees = JSON.parse((evt as MessageEvent).data) as { texte: string };
      mettreAJourDernierMessage((m) => ({ ...m, texte: m.texte + donnees.texte }));
    });

    source.addEventListener("termine", (evt) => {
      const donnees = JSON.parse((evt as MessageEvent).data) as { sourcee: boolean; sources: string[] };
      mettreAJourDernierMessage((m) => ({ ...m, sourcee: donnees.sourcee, sources: donnees.sources }));
      source.close();
      setEnCours(false);
    });

    source.onerror = () => {
      source.close();
      setEnCours(false);
    };
  }

  return (
    <div className="flex h-[70vh] w-full max-w-2xl flex-col rounded-lg border border-slate-200 bg-white shadow-sm">
      <div className="flex-1 space-y-4 overflow-y-auto p-4">
        {messages.length === 0 && (
          <p className="text-center text-sm text-slate-400">
            Posez une question sur les politiques internes ou sur l&apos;avancement de votre
            dossier.
          </p>
        )}
        {messages.map((message, index) => (
          <div key={index} className={message.role === "user" ? "text-right" : "text-left"}>
            <div
              className={`inline-block max-w-md rounded-lg px-4 py-2 text-left text-sm ${
                message.role === "user"
                  ? "bg-slate-900 text-white"
                  : "border border-slate-200 bg-slate-50 text-slate-900"
              }`}
            >
              <div className="prose prose-sm max-w-none prose-p:my-1">
                <ReactMarkdown>{message.texte || "…"}</ReactMarkdown>
              </div>
              {message.role === "assistant" && message.sources && message.sources.length > 0 && (
                <p className="mt-2 border-t border-slate-200 pt-1 text-xs text-slate-400">
                  Source{message.sources.length > 1 ? "s" : ""} : {message.sources.join(", ")}
                </p>
              )}
            </div>
          </div>
        ))}
      </div>

      <form onSubmit={envoyer} className="flex gap-2 border-t border-slate-200 p-4">
        <input
          type="text"
          value={question}
          onChange={(e) => setQuestion(e.target.value)}
          disabled={enCours}
          placeholder="Posez votre question..."
          className="flex-1 rounded-md border border-slate-300 px-3 py-2 text-sm focus:border-slate-500 focus:outline-none disabled:opacity-50"
        />
        <button
          type="submit"
          disabled={enCours || !question.trim()}
          className="rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white transition hover:bg-slate-700 disabled:opacity-50"
        >
          {enCours ? "…" : "Envoyer"}
        </button>
      </form>
    </div>
  );
}
