/*
 * This file is part of StreamActions.
 * Copyright © 2019-2026 StreamActions Team (streamactions.github.io)
 *
 * StreamActions is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * StreamActions is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with StreamActions.  If not, see <https://www.gnu.org/licenses/>.
 */

(function () {
    "use strict";

    function toBase64Url(buffer) {
        const bytes = new Uint8Array(buffer);
        let binary = "";
        for (let i = 0; i < bytes.byteLength; i++) {
            binary += String.fromCharCode(bytes[i]);
        }

        return btoa(binary)
            .replace(/\+/g, "-")
            .replace(/\//g, "_")
            .replace(/=+$/g, "");
    }

    function serializeCreationCredential(credential) {
        if (typeof credential.toJSON === "function") {
            return credential.toJSON();
        }

        const response = credential.response;
        const transports = typeof response.getTransports === "function" ? response.getTransports() : undefined;

        return {
            id: credential.id,
            rawId: toBase64Url(credential.rawId),
            type: credential.type,
            authenticatorAttachment: credential.authenticatorAttachment || undefined,
            clientExtensionResults: typeof credential.getClientExtensionResults === "function"
                ? credential.getClientExtensionResults()
                : {},
            response: {
                clientDataJSON: toBase64Url(response.clientDataJSON),
                attestationObject: toBase64Url(response.attestationObject),
                transports: transports
            }
        };
    }

    function serializeAssertionCredential(credential) {
        if (typeof credential.toJSON === "function") {
            return credential.toJSON();
        }

        const response = credential.response;
        return {
            id: credential.id,
            rawId: toBase64Url(credential.rawId),
            type: credential.type,
            authenticatorAttachment: credential.authenticatorAttachment || undefined,
            clientExtensionResults: typeof credential.getClientExtensionResults === "function"
                ? credential.getClientExtensionResults()
                : {},
            response: {
                clientDataJSON: toBase64Url(response.clientDataJSON),
                authenticatorData: toBase64Url(response.authenticatorData),
                signature: toBase64Url(response.signature),
                userHandle: response.userHandle ? toBase64Url(response.userHandle) : null
            }
        };
    }

    function setErrorElement(errorElementId, message) {
        if (!errorElementId) {
            return;
        }

        const el = document.getElementById(errorElementId);
        if (el) {
            el.textContent = message || "";
            el.hidden = !message;
        }
    }

    function ensureWebAuthnSupport() {
        if (!window.PublicKeyCredential || !navigator.credentials) {
            throw new Error("WebAuthn passkeys are not supported by this browser.");
        }
    }

    async function readErrorMessage(response, fallback) {
        try {
            const text = await response.text();
            return text && text.trim().length > 0 ? text.trim() : fallback;
        } catch (_err) {
            return fallback;
        }
    }

    async function registerPasskey(name, returnUrl) {
        ensureWebAuthnSupport();

        const optionsResponse = await fetch("/auth/mfa/passkey/create-options", {
            method: "POST",
            credentials: "same-origin",
            headers: {
                "Accept": "application/json"
            }
        });

        if (!optionsResponse.ok) {
            throw new Error(await readErrorMessage(optionsResponse, "Failed to obtain passkey creation options."));
        }

        const optionsJson = await optionsResponse.json();
        const publicKey = PublicKeyCredential.parseCreationOptionsFromJSON(optionsJson);
        const credential = await navigator.credentials.create({ publicKey: publicKey });
        if (!credential) {
            throw new Error("Passkey creation was cancelled.");
        }

        const serialized = serializeCreationCredential(credential);
        const registerResponse = await fetch("/auth/mfa/passkey/register", {
            method: "POST",
            credentials: "same-origin",
            headers: {
                "Content-Type": "application/json",
                "Accept": "application/json"
            },
            body: JSON.stringify({
                credentialJson: JSON.stringify(serialized),
                name: name && name.trim().length > 0 ? name.trim() : null,
                returnUrl: returnUrl || null
            })
        });

        if (!registerResponse.ok) {
            throw new Error(await readErrorMessage(registerResponse, "Passkey registration failed."));
        }

        const result = await registerResponse.json();
        const targetUrl = returnUrl && returnUrl.trim().length > 0 ? returnUrl.trim() : "/account/mfa";
        window.location.assign(targetUrl);
        return result;
    }

    async function verifyPasskey(returnUrl, errorElementId, workspaceId) {
        setErrorElement(errorElementId, "");

        try {
            ensureWebAuthnSupport();

            const optionsUrl = workspaceId && workspaceId.trim().length > 0
                ? `/auth/mfa/passkey/request-options?workspaceId=${encodeURIComponent(workspaceId.trim())}`
                : "/auth/mfa/passkey/request-options";

            const optionsResponse = await fetch(optionsUrl, {
                method: "POST",
                credentials: "same-origin",
                headers: {
                    "Accept": "application/json"
                }
            });

            if (!optionsResponse.ok) {
                throw new Error(await readErrorMessage(optionsResponse, "Failed to obtain passkey challenge options."));
            }

            const optionsJson = await optionsResponse.json();
            const publicKey = PublicKeyCredential.parseRequestOptionsFromJSON(optionsJson);
            const credential = await navigator.credentials.get({ publicKey: publicKey });
            if (!credential) {
                throw new Error("Passkey verification was cancelled.");
            }

            const serialized = serializeAssertionCredential(credential);
            const verifyResponse = await fetch("/auth/mfa/passkey/verify", {
                method: "POST",
                credentials: "same-origin",
                headers: {
                    "Content-Type": "application/json",
                    "Accept": "application/json"
                },
                body: JSON.stringify({
                    credentialJson: JSON.stringify(serialized),
                    returnUrl: returnUrl || null,
                    workspaceId: workspaceId && workspaceId.trim().length > 0 ? workspaceId.trim() : null
                })
            });

            if (!verifyResponse.ok) {
                throw new Error(await readErrorMessage(verifyResponse, "Passkey verification failed."));
            }

            const result = await verifyResponse.json();
            const targetUrl = (result && result.returnUrl) || returnUrl || "/";
            window.location.assign(targetUrl);
            return result;
        } catch (err) {
            const message = err && err.message ? err.message : "Passkey verification failed.";
            setErrorElement(errorElementId, message);
            throw err;
        }
    }

    async function handleJsonRedirectOrThrow(response, fallbackMessage) {
        let rawText = "";
        try {
            rawText = await response.text();
            if (rawText) {
                const parsed = JSON.parse(rawText);
                if (parsed && parsed.redirectUrl) {
                    window.location.assign(parsed.redirectUrl);
                    return true;
                }
            }
        } catch (_err) {
            // Fall through to throw raw text or fallback message.
        }

        throw new Error(rawText && rawText.trim().length > 0 ? rawText.trim() : fallbackMessage);
    }

    async function loginWithPasskey(returnUrl, errorElementId) {
        setErrorElement(errorElementId, "");

        try {
            ensureWebAuthnSupport();

            const optionsResponse = await fetch("/auth/passkey/request-options", {
                method: "POST",
                credentials: "same-origin",
                headers: {
                    "Accept": "application/json"
                }
            });

            if (!optionsResponse.ok) {
                const redirected = await handleJsonRedirectOrThrow(optionsResponse, "Failed to obtain passkey sign-in options.");
                if (redirected) {
                    return null;
                }
            }

            const optionsJson = await optionsResponse.json();
            const publicKey = PublicKeyCredential.parseRequestOptionsFromJSON(optionsJson);
            const credential = await navigator.credentials.get({ publicKey: publicKey });
            if (!credential) {
                throw new Error("Passkey sign-in was cancelled.");
            }

            const serialized = serializeAssertionCredential(credential);
            const loginResponse = await fetch("/auth/passkey/login", {
                method: "POST",
                credentials: "same-origin",
                headers: {
                    "Content-Type": "application/json",
                    "Accept": "application/json"
                },
                body: JSON.stringify({
                    credentialJson: JSON.stringify(serialized),
                    returnUrl: returnUrl || null
                })
            });

            if (!loginResponse.ok) {
                const redirected = await handleJsonRedirectOrThrow(loginResponse, "Passkey sign-in failed.");
                if (redirected) {
                    return null;
                }
            }

            const result = await loginResponse.json();
            const targetUrl = (result && result.returnUrl) || returnUrl || "/";
            window.location.assign(targetUrl);
            return result;
        } catch (err) {
            const message = err && err.message ? err.message : "Passkey sign-in failed.";
            setErrorElement(errorElementId, message);
            throw err;
        }
    }

    async function beginTotpEnrollment() {
        const response = await fetch("/auth/mfa/totp/enroll/begin", {
            method: "POST",
            credentials: "same-origin",
            headers: {
                "Accept": "application/json"
            }
        });

        if (!response.ok) {
            throw new Error(await readErrorMessage(response, "Unable to start TOTP enrollment."));
        }

        return await response.json();
    }

    async function confirmTotpEnrollment(code) {
        const response = await fetch("/auth/mfa/totp/enroll/confirm", {
            method: "POST",
            credentials: "same-origin",
            headers: {
                "Content-Type": "application/json",
                "Accept": "application/json"
            },
            body: JSON.stringify({ code: code })
        });

        if (!response.ok) {
            throw new Error(await readErrorMessage(response, "Invalid TOTP verification code."));
        }

        return await response.json();
    }

    function showMfaMethod(containerSelector, method) {
        const container = containerSelector
            ? document.querySelector(containerSelector)
            : document;
        if (!container) {
            return;
        }

        const totpForm = container.querySelector("form[data-mfa-totp], form[data-step-up-totp]");
        const recoveryForm = container.querySelector("form[data-mfa-recovery], form[data-step-up-recovery]");

        const inputs = container.querySelectorAll(
            "form[data-mfa-totp] input[name='code'], form[data-step-up-totp] input[name='code'], " +
            "form[data-mfa-recovery] input[name='code'], form[data-step-up-recovery] input[name='code']"
        );
        for (let i = 0; i < inputs.length; i++) {
            inputs[i].value = "";
        }

        if (totpForm) {
            totpForm.hidden = method !== "totp";
        }
        if (recoveryForm) {
            recoveryForm.hidden = method !== "recovery";
        }

        const totpToggle = container.querySelector("[data-mfa-toggle-totp], [data-step-up-toggle-totp]");
        const recoveryToggle = container.querySelector("[data-mfa-toggle-recovery], [data-step-up-toggle-recovery]");
        if (totpToggle) {
            totpToggle.setAttribute("aria-expanded", method === "totp" ? "true" : "false");
        }
        if (recoveryToggle) {
            recoveryToggle.setAttribute("aria-expanded", method === "recovery" ? "true" : "false");
        }

        if (method === "totp" && totpForm) {
            const totpInput = totpForm.querySelector("input[name='code']");
            if (totpInput) {
                totpInput.focus();
            }
        } else if (method === "recovery" && recoveryForm) {
            const recoveryInput = recoveryForm.querySelector("input[name='code']");
            if (recoveryInput) {
                recoveryInput.focus();
            }
        }
    }

    document.addEventListener("click", function (event) {
        const trigger = event.target && typeof event.target.closest === "function"
            ? event.target.closest("[data-mfa-target-method]")
            : null;
        if (!trigger) {
            return;
        }

        const containerSelector = trigger.getAttribute("data-mfa-container");
        const method = trigger.getAttribute("data-mfa-target-method");
        if (method) {
            showMfaMethod(containerSelector, method);
        }
    });

    window.streamActionsWebAuthn = {
        registerPasskey: registerPasskey,
        verifyPasskey: verifyPasskey,
        loginWithPasskey: loginWithPasskey,
        beginTotpEnrollment: beginTotpEnrollment,
        confirmTotpEnrollment: confirmTotpEnrollment,
        showMfaMethod: showMfaMethod
    };
})();

