export interface Env {
	ANTHROPIC_API_KEY: string;
	ASSEMBLYAI_API_KEY: string;
	ELEVENLABS_API_KEY: string;
	CLAUDE_MODEL: string;
	POINTYPAL_CLIENT_KEY: string;
	ALLOWED_ORIGINS: string;
	DEFAULT_CLAUDE_MODEL: string;
	MAX_CHAT_REQUESTS_PER_DAY?: string;
	MAX_TRANSCRIBE_REQUESTS_PER_DAY?: string;
	MAX_TTS_REQUESTS_PER_DAY?: string;
}

const ALLOWED_MODELS = [
	'claude-3-5-sonnet-20240620',
	'claude-3-5-sonnet-latest',
	'claude-3-sonnet-20240229',
	'claude-3-opus-20240229',
	'claude-3-haiku-20240307',
	'claude-sonnet-4-5',
	'claude-sonnet-4-6'
];

export default {
	async fetch(request: Request, env: Env, ctx: ExecutionContext): Promise<Response> {
		const url = new URL(request.url);
		const requestId = crypto.randomUUID();

		// CORS setup
		const origin = request.headers.get('Origin');
		const allowedOrigins = env.ALLOWED_ORIGINS ? env.ALLOWED_ORIGINS.split(',').map(o => o.trim()) : [];
		
		let accessControlAllowOrigin = '*';
		if (allowedOrigins.length > 0) {
			if (origin && allowedOrigins.includes(origin)) {
				accessControlAllowOrigin = origin;
			} else if (!origin) {
				// Allow requests without Origin (e.g. desktop apps)
				accessControlAllowOrigin = '*';
			} else {
				accessControlAllowOrigin = allowedOrigins[0]; // Fallback to first allowed
			}
		}

		const corsHeaders: Record<string, string> = {
			'Access-Control-Allow-Origin': accessControlAllowOrigin,
			'Access-Control-Allow-Methods': 'GET, POST, OPTIONS',
			'Access-Control-Allow-Headers': 'Content-Type, X-PointyPal-Client-Key',
		};

		if (request.method === 'OPTIONS') {
			return new Response(null, { headers: corsHeaders });
		}

		// Auth check for protected routes
		const protectedRoutes = ['/chat', '/transcribe', '/tts'];
		if (protectedRoutes.includes(url.pathname)) {
			const clientKey = request.headers.get('X-PointyPal-Client-Key');
			if (!env.POINTYPAL_CLIENT_KEY || clientKey !== env.POINTYPAL_CLIENT_KEY) {
				console.warn(`[${requestId}] Unauthorized request to ${url.pathname}`);
				return new Response(JSON.stringify({ 
					error: 'unauthorized', 
					status: 401, 
					requestId 
				}), {
					status: 401,
					headers: { ...corsHeaders, 'Content-Type': 'application/json' },
				});
			}
		}

		// GET /health
		if (url.pathname === '/health' && request.method === 'GET') {
			return new Response(JSON.stringify({ 
				ok: true, 
				service: 'pointypal-worker',
				version: '1.1.0',
				timestamp: new Date().toISOString(),
				features: {
					chat: true,
					transcribe: true,
					tts: true
				}
			}), {
				headers: { ...corsHeaders, 'Content-Type': 'application/json' },
			});
		}

		// GET /version
		if (url.pathname === '/version' && request.method === 'GET') {
			return new Response(JSON.stringify({
				service: 'pointypal-worker',
				version: '1.1.0',
				buildDate: '2026-04-27',
				requestId
			}), {
				headers: { ...corsHeaders, 'Content-Type': 'application/json' },
			});
		}

		// POST /tts
		if (url.pathname === '/tts' && request.method === 'POST') {
			try {
				const body: any = await request.json();
				const text = body.text;
				const voiceId = body.voiceId;
				const modelId = body.modelId || 'eleven_flash_v2_5';
				const outputFormat = body.outputFormat || 'mp3_44100_128';

				// Validation
				if (!text) {
					return new Response(JSON.stringify({ error: 'validation_failed', message: 'text is required', status: 400, requestId }), {
						status: 400,
						headers: { ...corsHeaders, 'Content-Type': 'application/json' },
					});
				}

				const maxTtsChars = parseInt(env.MAX_TTS_CHARS || '700');
				if (text.length > maxTtsChars) {
					return new Response(JSON.stringify({ error: 'validation_failed', message: `text too long (max ${maxTtsChars})`, status: 400, requestId }), {
						status: 400,
						headers: { ...corsHeaders, 'Content-Type': 'application/json' },
					});
				}

				if (!voiceId) {
					return new Response(JSON.stringify({ error: 'validation_failed', message: 'voiceId is required', status: 400, requestId }), {
						status: 400,
						headers: { ...corsHeaders, 'Content-Type': 'application/json' },
					});
				}

				const allowedTtsModels = ['eleven_flash_v2_5', 'eleven_turbo_v2_5'];
				if (!allowedTtsModels.includes(modelId)) {
					return new Response(JSON.stringify({ error: 'validation_failed', message: 'modelId not allowed', status: 400, requestId }), {
						status: 400,
						headers: { ...corsHeaders, 'Content-Type': 'application/json' },
					});
				}

				if (outputFormat !== 'mp3_44100_128') {
					return new Response(JSON.stringify({ error: 'validation_failed', message: 'outputFormat not allowed', status: 400, requestId }), {
						status: 400,
						headers: { ...corsHeaders, 'Content-Type': 'application/json' },
					});
				}

				if (!env.ELEVENLABS_API_KEY) {
					return new Response(JSON.stringify({ error: 'provider_error', message: 'Worker missing ELEVENLABS_API_KEY', status: 500, requestId }), {
						status: 500,
						headers: { ...corsHeaders, 'Content-Type': 'application/json' },
					});
				}

				console.log(`[${requestId}] TTS request: len=${text.length}, voice=${voiceId}, model=${modelId}`);

				const ttsUrl = `https://api.elevenlabs.io/v1/text-to-speech/${voiceId}?output_format=${outputFormat}`;
				
				const ttsResponse = await fetch(ttsUrl, {
					method: 'POST',
					headers: {
						'xi-api-key': env.ELEVENLABS_API_KEY,
						'Content-Type': 'application/json',
					},
					body: JSON.stringify({
						text: text,
						model_id: modelId,
					}),
				});

				if (!ttsResponse.ok) {
					const errorData: any = await ttsResponse.json().catch(() => ({ detail: { message: 'Unknown ElevenLabs error' } }));
					const errMsg = errorData.detail?.message || 'ElevenLabs API error';
					return new Response(JSON.stringify({ error: 'provider_error', message: errMsg, status: ttsResponse.status, requestId }), {
						status: ttsResponse.status,
						headers: { ...corsHeaders, 'Content-Type': 'application/json' },
					});
				}

				const audioBuffer = await ttsResponse.arrayBuffer();
				const audioBase64 = btoa(String.fromCharCode(...new Uint8Array(audioBuffer)));

				return new Response(JSON.stringify({
					audioBase64: audioBase64,
					audioMimeType: 'audio/mpeg',
					provider: 'elevenlabs',
					durationMs: 0,
					requestId
				}), {
					headers: { ...corsHeaders, 'Content-Type': 'application/json' },
				});

			} catch (err: any) {
				console.error(`[${requestId}] TTS Error:`, err);
				return new Response(JSON.stringify({ error: 'internal_error', message: err.message, status: 500, requestId }), {
					status: 500,
					headers: { ...corsHeaders, 'Content-Type': 'application/json' },
				});
			}
		}

		// POST /transcribe
		if (url.pathname === '/transcribe' && request.method === 'POST') {
			try {
				const body: any = await request.json();
				const audioBase64 = body.audioBase64;
				const language = body.language || 'pl';

				// Validation
				if (!audioBase64) {
					return new Response(JSON.stringify({ error: 'validation_failed', message: 'audioBase64 is required', status: 400, requestId }), {
						status: 400,
						headers: { ...corsHeaders, 'Content-Type': 'application/json' },
					});
				}

				const maxAudioBase64Length = 20 * 1024 * 1024; // ~15MB binary
				if (audioBase64.length > maxAudioBase64Length) {
					return new Response(JSON.stringify({ error: 'validation_failed', message: 'audio data too large', status: 400, requestId }), {
						status: 400,
						headers: { ...corsHeaders, 'Content-Type': 'application/json' },
					});
				}

				const allowedLanguages = ['pl', 'en', 'auto'];
				if (!allowedLanguages.includes(language)) {
					return new Response(JSON.stringify({ error: 'validation_failed', message: 'language not allowed', status: 400, requestId }), {
						status: 400,
						headers: { ...corsHeaders, 'Content-Type': 'application/json' },
					});
				}

				if (!env.ASSEMBLYAI_API_KEY) {
					return new Response(JSON.stringify({ error: 'provider_error', message: 'Worker missing ASSEMBLYAI_API_KEY', status: 500, requestId }), {
						status: 500,
						headers: { ...corsHeaders, 'Content-Type': 'application/json' },
					});
				}

				console.log(`[${requestId}] Transcribe request: size=${audioBase64.length}, lang=${language}`);

				const audioBuffer = Uint8Array.from(atob(audioBase64), c => c.charCodeAt(0));

				// 1. Upload to AssemblyAI
				const uploadResponse = await fetch('https://api.assemblyai.com/v2/upload', {
					method: 'POST',
					headers: {
						'Authorization': env.ASSEMBLYAI_API_KEY,
						'Content-Type': 'application/octet-stream',
					},
					body: audioBuffer,
				});

				if (!uploadResponse.ok) {
					const errorText = await uploadResponse.text();
					throw new Error(`AssemblyAI Upload Failed: ${errorText}`);
				}

				const uploadData: any = await uploadResponse.json();
				const uploadUrl = uploadData.upload_url;

				// 2. Start Transcription
				const transcriptResponse = await fetch('https://api.assemblyai.com/v2/transcript', {
					method: 'POST',
					headers: {
						'Authorization': env.ASSEMBLYAI_API_KEY,
						'Content-Type': 'application/json',
					},
					body: JSON.stringify({
						audio_url: uploadUrl,
						language_code: language === 'auto' ? undefined : language,
						language_detection: language === 'auto',
					}),
				});

				if (!transcriptResponse.ok) {
					const errorText = await transcriptResponse.text();
					throw new Error(`AssemblyAI Transcription Start Failed: ${errorText}`);
				}

				const transcriptData: any = await transcriptResponse.json();
				const transcriptId = transcriptData.id;

				// 3. Poll for Completion
				let text = "";
				const startTime = Date.now();
				const maxWaitMs = 25000;

				while (Date.now() - startTime < maxWaitMs) {
					const pollResponse = await fetch(`https://api.assemblyai.com/v2/transcript/${transcriptId}`, {
						headers: { 'Authorization': env.ASSEMBLYAI_API_KEY },
					});

					const pollData: any = await pollResponse.json();

					if (pollData.status === 'completed') {
						text = pollData.text;
						break;
					} else if (pollData.status === 'error') {
						throw new Error(`AssemblyAI Error: ${pollData.error}`);
					}

					await new Promise(resolve => setTimeout(resolve, 1000));
				}

				if (!text) {
					throw new Error("Transcription timed out in worker.");
				}

				return new Response(JSON.stringify({ 
					text: text,
					provider: "assemblyai",
					durationMs: Date.now() - startTime,
					requestId
				}), {
					headers: { ...corsHeaders, 'Content-Type': 'application/json' },
				});

			} catch (err: any) {
				console.error(`[${requestId}] Transcribe Error:`, err);
				return new Response(JSON.stringify({ error: 'internal_error', message: err.message, status: 500, requestId }), {
					status: 500,
					headers: { ...corsHeaders, 'Content-Type': 'application/json' },
				});
			}
		}

		// POST /chat
		if (url.pathname === '/chat' && request.method === 'POST') {
			try {
				const body: any = await request.json();
				
				// Validation
				if (!body.screenshotBase64 && !body.userText) {
					return new Response(JSON.stringify({ error: 'validation_failed', message: 'Missing both screenshot and text', status: 400, requestId }), {
						status: 400,
						headers: { ...corsHeaders, 'Content-Type': 'application/json' },
					});
				}

				if (body.userText && body.userText.length > 4000) {
					return new Response(JSON.stringify({ error: 'validation_failed', message: 'userText too long', status: 400, requestId }), {
						status: 400,
						headers: { ...corsHeaders, 'Content-Type': 'application/json' },
					});
				}

				if (body.screenshotBase64) {
					const maxScreenshotLength = 10 * 1024 * 1024; // 10MB base64
					if (body.screenshotBase64.length > maxScreenshotLength) {
						return new Response(JSON.stringify({ error: 'validation_failed', message: 'screenshotBase64 too large', status: 400, requestId }), {
							status: 400,
							headers: { ...corsHeaders, 'Content-Type': 'application/json' },
						});
					}

					const allowedMimeTypes = ['image/jpeg', 'image/png'];
					if (body.screenshotMimeType && !allowedMimeTypes.includes(body.screenshotMimeType)) {
						return new Response(JSON.stringify({ error: 'validation_failed', message: 'invalid screenshot mime type', status: 400, requestId }), {
							status: 400,
							headers: { ...corsHeaders, 'Content-Type': 'application/json' },
						});
					}

					if (body.screenshotWidth <= 0 || body.screenshotWidth > 4096 || body.screenshotHeight <= 0 || body.screenshotHeight > 4096) {
						return new Response(JSON.stringify({ error: 'validation_failed', message: 'invalid screenshot dimensions', status: 400, requestId }), {
							status: 400,
							headers: { ...corsHeaders, 'Content-Type': 'application/json' },
						});
					}
				}

				const model = body.model || env.DEFAULT_CLAUDE_MODEL || env.CLAUDE_MODEL || 'claude-3-5-sonnet-20240620';
				if (!ALLOWED_MODELS.includes(model)) {
					return new Response(JSON.stringify({ error: 'model_not_allowed', message: `Model ${model} is not in allowlist`, status: 400, requestId }), {
						status: 400,
						headers: { ...corsHeaders, 'Content-Type': 'application/json' },
					});
				}

				const interactionMode = body.interactionMode || 'Assist';
				const allowedInteractionModes = ['Assist', 'Direct', 'Creative'];
				const safeInteractionMode = allowedInteractionModes.includes(interactionMode) ? interactionMode : 'Assist';

				const profileInstructions = body.promptProfileInstructions || '';
				const safeProfileInstructions = profileInstructions.substring(0, 2000);
				
				console.log(`[${requestId}] Chat request: model=${model}, mode=${safeInteractionMode}, textLen=${body.userText?.length || 0}`);

				const systemPrompt = `Jesteś PointyPal, pomocnym asystentem kursora w systemie Windows.
Twoim zadaniem jest odpowiadanie na pytania użytkownika dotyczące tego, co widzi na ekranie.

Zasady:
1. Odpowiadaj zawsze w języku polskim.
2. Bądź bardzo zwięzły i naturalny.
3. Tryb interakcji: ${safeInteractionMode}
${safeProfileInstructions}
4. ZAWSZE dołączaj dokładnie jeden tag punktu na samym końcu swojej odpowiedzi.
5. Jeśli wskazanie czegoś pomoże, użyj: [POINT:x,y:etykieta].
6. Jeśli nie ma nic do wskazania, użyj: [POINT:none].
7. Współrzędne MUSZĄ znajdować się wewnątrz wymiarów zrzutu ekranu i celować w środek kontrolki.
8. Tag punktu musi być ostatnim tekstem w Twojej odpowiedzi.
9. Nigdy nie dołączaj więcej niż jednego tagu.
10. Nie używaj markdown (pogrubienia, listy, kody) - tylko czysty tekst.

Informacje o kontekście:
- Rozdzielczość zrzutu: ${body.screenshotWidth || 'N/A'}x${body.screenshotHeight || 'N/A'}
- Pozycja kursora na zrzucie: ${body.cursorImagePosition?.x || 'N/A'}, ${body.cursorImagePosition?.y || 'N/A'}
- Obszar monitora: left=${body.monitorBounds?.left}, top=${body.monitorBounds?.top}, width=${body.monitorBounds?.width}, height=${body.monitorBounds?.height}

Instrukcje dodatkowe: ${body.instructions || 'Brak'}`;

				let uiContextText = "";
				if (body.uiAutomationContext && body.uiAutomationContext.isAvailable) {
					const ctx = body.uiAutomationContext;
					uiContextText = `\n\nWindows UI Automation Context:
- Active Window: ${ctx.activeWindowTitle || 'Unknown'}
- Focused Element: ${ctx.focusedElement?.name || 'None'} (${ctx.focusedElement?.localizedControlType || 'Unknown'})
- Element Under Cursor: ${ctx.elementUnderCursor?.name || 'None'} (${ctx.elementUnderCursor?.localizedControlType || 'Unknown'})`;

					if (ctx.nearbyElements && ctx.nearbyElements.length > 0) {
						uiContextText += `\n- Nearby Elements:\n  ${ctx.nearbyElements.slice(0, 20).map((e: any) => 
							`* ${e.name || e.automationId || 'Unnamed'} (${e.localizedControlType}): [${Math.round(e.boundingRectangle.left)},${Math.round(e.boundingRectangle.top)},${Math.round(e.boundingRectangle.width)}x${Math.round(e.boundingRectangle.height)}]`
						).join('\n  ')}`;
					}
				}

				const content: any[] = [];
				if (body.screenshotBase64) {
					content.push({
						type: 'image',
						source: {
							type: 'base64',
							media_type: body.screenshotMimeType || 'image/jpeg',
							data: body.screenshotBase64,
						},
					});
				}
				
				content.push({
					type: 'text',
					text: (body.userText || 'Co widzisz na tym obrazku?') + uiContextText.substring(0, 5000),
				});

				const anthropicRequest = {
					model: model,
					max_tokens: 500,
					system: systemPrompt,
					messages: [
						{
							role: 'user',
							content: content,
						},
					],
				};

				const response = await fetch('https://api.anthropic.com/v1/messages', {
					method: 'POST',
					headers: {
						'Content-Type': 'application/json',
						'x-api-key': env.ANTHROPIC_API_KEY,
						'anthropic-version': '2023-06-01',
					},
					body: JSON.stringify(anthropicRequest),
				});

				if (!response.ok) {
					const errorData: any = await response.json();
					return new Response(JSON.stringify({ error: 'provider_error', message: errorData.error?.message || 'Anthropic API error', status: response.status, requestId }), {
						status: response.status,
						headers: { ...corsHeaders, 'Content-Type': 'application/json' },
					});
				}

				const data: any = await response.json();
				const text = data.content?.[0]?.text || 'Nie udało mi się odczytać odpowiedzi. [POINT:none]';

				return new Response(JSON.stringify({ text: text, requestId }), {
					headers: { ...corsHeaders, 'Content-Type': 'application/json' },
				});

			} catch (err: any) {
				console.error(`[${requestId}] Chat Error:`, err);
				return new Response(JSON.stringify({ error: 'internal_error', message: err.message, status: 400, requestId }), {
					status: 400,
					headers: { ...corsHeaders, 'Content-Type': 'application/json' },
				});
			}
		}

		// Fallback
		return new Response(JSON.stringify({ error: 'unsupported_route', message: `Method ${request.method} on ${url.pathname} not supported`, status: 404, requestId }), {
			status: 404,
			headers: { ...corsHeaders, 'Content-Type': 'application/json' },
		});
	},
};
