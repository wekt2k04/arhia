import { NextResponse } from "next/server";
import { effacerSession } from "@/lib/api/session";

export async function POST() {
  await effacerSession();
  return NextResponse.json({ success: true });
}
